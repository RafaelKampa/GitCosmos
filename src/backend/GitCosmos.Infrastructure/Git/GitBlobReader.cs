using System.ComponentModel;
using System.Diagnostics;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.Interfaces.Git;
namespace GitCosmos.Infrastructure.Git;

public class GitBlobReader(string executable, TimeSpan timeout) : IGitBlobReader
{
    public const int MaximumBytes = 2_000_000;
    private static readonly SemaphoreSlim Slots = new(2, 2);
    public async Task<byte[]> ReadAsync(string root, string oid, CancellationToken cancellationToken)
    {
        await Slots.WaitAsync(cancellationToken);
        try { return await ReadCoreAsync(root, oid, cancellationToken); }
        finally { Slots.Release(); }
    }
    private async Task<byte[]> ReadCoreAsync(string root, string oid, CancellationToken cancellationToken)
    {
        if (!GitHistoryParser.IsSha(oid)) throw new ValidationAppException(LocalizedText.Get("history.invalid"));
        using var limit = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(limit.Token, cancellationToken);
        linked.Token.ThrowIfCancellationRequested();
        using var process = new Process { StartInfo = new(executable) { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true } };
        foreach (var key in process.StartInfo.Environment.Keys.Where(key => key.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToArray()) process.StartInfo.Environment.Remove(key);
        process.StartInfo.Environment["GIT_TERMINAL_PROMPT"] = "0"; process.StartInfo.Environment["GIT_NO_LAZY_FETCH"] = "1";
        foreach (var arg in new[] { "cat-file", "blob", oid }) process.StartInfo.ArgumentList.Add(arg);
        try { process.Start(); } catch (Win32Exception) { throw Failed(); }
        process.StandardInput.Close();
        var errors = DrainAsync(process.StandardError.BaseStream);
        using var output = new MemoryStream();
        try
        {
            var buffer = new byte[8192]; int count;
            while ((count = await process.StandardOutput.BaseStream.ReadAsync(buffer, linked.Token)) > 0)
            {
                if (output.Length + count > MaximumBytes) throw new ValidationAppException(LocalizedText.Get("commit.limit"));
                output.Write(buffer, 0, count);
            }
            await process.WaitForExitAsync(linked.Token); await errors;
            if (process.ExitCode != 0) throw Failed();
            return output.ToArray();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new OperationFailedAppException(LocalizedText.Get("repository.git_timeout"));
        }
        finally
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            await process.WaitForExitAsync(CancellationToken.None); await errors;
        }
    }
    private static async Task DrainAsync(Stream stream) { var buffer = new byte[4096]; while (await stream.ReadAsync(buffer) > 0) { } }
    private static OperationFailedAppException Failed() => new(LocalizedText.Get("history.failed"));
}
