using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.Interfaces.Git;

namespace GitCosmos.Infrastructure.Git;

public class GitProcessRunner(string executable, TimeSpan timeout) : IGitProcessRunner
{
    public async Task<GitCommandResult> RunAsync(string directory, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
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
        foreach (var variable in new[] { "GIT_DIR", "GIT_WORK_TREE", "GIT_COMMON_DIR", "GIT_INDEX_FILE", "GIT_OBJECT_DIRECTORY", "GIT_ALTERNATE_OBJECT_DIRECTORIES" })
            process.StartInfo.Environment.Remove(variable);
        process.StartInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        process.StartInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        try
        {
            process.Start();
        }
        catch (Win32Exception)
        {
            throw new OperationFailedAppException(LocalizedText.Get("repository.git_unavailable"));
        }
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(linked.Token);
            await Task.WhenAll(output, errors);
            return new(process.ExitCode, await output);
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
}
