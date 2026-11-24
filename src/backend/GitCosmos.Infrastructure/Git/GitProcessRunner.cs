using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.Interfaces.Git;

namespace GitCosmos.Infrastructure.Git;

public class GitProcessRunner(string executable, TimeSpan timeout) : IGitProcessRunner, IBoundedGitProcessRunner
{
    public async Task<GitCommandResult> RunAsync(string directory, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        var result = await RunLimitedAsync(directory, arguments, int.MaxValue, cancellationToken);
        return new(result.ExitCode, result.Output);
    }

    public async Task<LimitedGitOutput> RunLimitedAsync(string directory, IReadOnlyList<string> arguments, int maximumCharacters, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumCharacters);
        cancellationToken.ThrowIfCancellationRequested();
        using var limit = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, limit.Token);
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(executable)
            {
                WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            }
        };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        // An inherited Git context must never redirect inspection to another repository.
        foreach (var variable in new[] { "GIT_DIR", "GIT_WORK_TREE", "GIT_COMMON_DIR", "GIT_INDEX_FILE", "GIT_OBJECT_DIRECTORY", "GIT_ALTERNATE_OBJECT_DIRECTORIES", "GIT_SEQUENCE_EDITOR", "GIT_EDITOR" })
            process.StartInfo.Environment.Remove(variable);
        process.StartInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        process.StartInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        // Read-only inspection must not fetch missing objects in partial clones.
        process.StartInfo.Environment["GIT_NO_LAZY_FETCH"] = "1";
        try
        {
            process.Start();
        }
        catch (Win32Exception)
        {
            throw new OperationFailedAppException(LocalizedText.Get("repository.git_unavailable"));
        }
        process.StandardInput.Close();
        var output = CaptureAsync(process.StandardOutput, maximumCharacters);
        var errors = CaptureAsync(process.StandardError, 0);
        try
        {
            await process.WaitForExitAsync(linked.Token);
            await Task.WhenAll(output, errors);
            var captured = await output;
            return new(process.ExitCode, captured.Text, captured.Truncated);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) { /* Process exited between the check and Kill. */ }
            await process.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(output, errors);
            if (cancellationToken.IsCancellationRequested) throw;
            throw new OperationFailedAppException(LocalizedText.Get("repository.git_timeout"));
        }
    }

    private static async Task<(string Text, bool Truncated)> CaptureAsync(StreamReader reader, int maximumCharacters)
    {
        var builder = new StringBuilder();
        var buffer = new char[4096];
        var truncated = false;
        int read;
        // Continue draining after reaching the bound so the child cannot block on a full pipe.
        while ((read = await reader.ReadAsync(buffer)) != 0)
        {
            var take = Math.Min(read, maximumCharacters - builder.Length);
            builder.Append(buffer, 0, take);
            if (take < read) truncated = true;
        }
        return (builder.ToString(), truncated);
    }
}
