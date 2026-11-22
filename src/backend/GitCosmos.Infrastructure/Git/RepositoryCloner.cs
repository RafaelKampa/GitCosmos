using System.Text.RegularExpressions;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.Interfaces.Git;
using Microsoft.Extensions.Logging;

namespace GitCosmos.Infrastructure.Git;

public class RepositoryCloner(IGitCloneRunner runner, IGitRepositoryInspector inspector, ILogger<RepositoryCloner> logger) : IRepositoryCloner
{
    public RepositoryClonePlan Plan(string remoteUrl, string parentPath)
    {
        var remote = remoteUrl.Trim();
        if (remote.Any(char.IsControl) || remote.Any(char.IsWhiteSpace))
            throw new ValidationAppException(LocalizedText.Get("clone.invalid_url"));
        string sourcePath;
        if (remote.Contains("://", StringComparison.Ordinal))
        {
            if (!Uri.TryCreate(remote, UriKind.Absolute, out var uri) || !new[] { "https", "http", "ssh", "git" }.Contains(uri.Scheme))
                throw new ValidationAppException(LocalizedText.Get("clone.invalid_url"));
            if (string.IsNullOrEmpty(uri.Host) || uri.Host.StartsWith('-') || uri.Query.Length > 0 || uri.Fragment.Length > 0
                || (uri.Scheme != "ssh" && uri.UserInfo.Length > 0)
                || (uri.UserInfo.Length > 0 && !Regex.IsMatch(uri.UserInfo, @"^[a-zA-Z0-9_][a-zA-Z0-9_.-]*$")))
                throw new ValidationAppException(LocalizedText.Get("clone.invalid_url"));
            sourcePath = uri.AbsolutePath;
        }
        else
        {
            if (Regex.IsMatch(remote, @"^[a-zA-Z]:[\\/]") || remote.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
                throw new ValidationAppException(LocalizedText.Get("clone.invalid_url"));
            var scp = Regex.Match(remote, @"^(?:[a-zA-Z0-9_][a-zA-Z0-9_.-]*@)?[a-zA-Z0-9][a-zA-Z0-9.-]*:([^:].*)$");
            if (!scp.Success) throw new ValidationAppException(LocalizedText.Get("clone.invalid_url"));
            sourcePath = scp.Groups[1].Value;
        }
        var name = Uri.UnescapeDataString(sourcePath.TrimEnd('/').Split('/')[^1]);
        if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        if (name.Length is 0 or > 255 || name.EndsWith(' ') || name.EndsWith('.') || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || Regex.IsMatch(name, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            throw new ValidationAppException(LocalizedText.Get("clone.invalid_url"));
        var parent = parentPath.Trim();
        if (!Path.IsPathFullyQualified(parent) || !Directory.Exists(parent))
            throw new ValidationAppException(LocalizedText.Get("repository.invalid_path"));
        parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent));
        var destination = Path.Combine(parent, name);
        EnsureAbsent(destination);
        return new(remote, parent, name, destination);
    }

    public async Task<LocalGitRepository> CloneAsync(RepositoryClonePlan plan, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var verified = Plan(plan.RemoteUrl, plan.ParentPath);
        if (!string.Equals(verified.DestinationPath, plan.DestinationPath, StringComparison.OrdinalIgnoreCase))
            throw new ValidationAppException(LocalizedText.Get("clone.destination_changed"));
        var staging = Path.Combine(verified.ParentPath, ".gitcosmos-clone-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(staging);
            var result = await runner.CloneAsync(verified.RemoteUrl, staging, cancellationToken);
            if (result.ExitCode != 0) throw new OperationFailedAppException(LocalizedText.Get("clone.failed"));
            cancellationToken.ThrowIfCancellationRequested();
            EnsureAbsent(verified.DestinationPath);
            try { Directory.Move(staging, verified.DestinationPath); }
            catch (IOException) when (Directory.Exists(verified.DestinationPath) || File.Exists(verified.DestinationPath))
            { throw new ConflictAppException(LocalizedText.Get("clone.destination_exists")); }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new OperationFailedAppException(LocalizedText.Get("clone.failed"));
        }
        finally
        {
            // Delete only the unique, owned staging directory; never the final repository.
            if (Directory.Exists(staging))
            {
                try { Cleanup(staging, verified.ParentPath); }
                catch (Exception exception) { logger.LogWarning(exception, "Clone staging cleanup failed: {StagingPath}", staging); }
            }
        }
        // The completed clone is preserved even if validation/linking is later interrupted.
        return await inspector.InspectAsync(verified.DestinationPath, cancellationToken);
    }

    private static void EnsureAbsent(string path)
    {
        if (Directory.Exists(path) || File.Exists(path))
            throw new ConflictAppException(LocalizedText.Get("clone.destination_exists"));
    }

    private static void Cleanup(string staging, string parent)
    {
        var full = Path.GetFullPath(staging);
        if (!string.Equals(Path.GetDirectoryName(full), parent, StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(full).StartsWith(".gitcosmos-clone-", StringComparison.Ordinal)
            || (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Not an owned clone staging directory.");
        var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };
        foreach (var file in Directory.EnumerateFiles(full, "*", options)) File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(full, recursive: true);
    }
}
