using System.Text;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.Interfaces.Git;

namespace GitCosmos.Infrastructure.Git;

public class RepositoryChangesReader(IBoundedGitProcessRunner runner, IGitRepositoryInspector inspector) : IRepositoryChangesReader
{
    public const int MaximumDiffCharacters = 200_000;
    private static readonly SemaphoreSlim Slots = new(2, 2);

    public async Task<GitChangesSnapshot> ReadAsync(string localPath, CancellationToken cancellationToken = default)
    {
        await Slots.WaitAsync(cancellationToken);
        try { return await ReadCoreAsync(localPath, cancellationToken); }
        finally { Slots.Release(); }
    }

    private async Task<GitChangesSnapshot> ReadCoreAsync(string localPath, CancellationToken cancellationToken)
    {
        var root = await inspector.InspectAsync(localPath, cancellationToken);
        if (!string.Equals(root.LocalPath, Path.TrimEndingDirectorySeparator(Path.GetFullPath(localPath)), StringComparison.OrdinalIgnoreCase))
            throw new ValidationAppException(LocalizedText.Get("dashboard.root_changed"));
        var result = await runner.RunLimitedAsync(localPath,
            ["-c", "core.fsmonitor=false", "status", "--porcelain=v2", "--branch", "-z", "--untracked-files=all", "--ignore-submodules=none", "--renames", "--no-ahead-behind"], 1_000_000, cancellationToken);
        if (result.ExitCode != 0) throw new OperationFailedAppException(LocalizedText.Get("dashboard.status_failed"));
        if (result.Truncated) throw new OperationFailedAppException(LocalizedText.Get("changes.list_limit"));
        return GitChangesParser.Parse(result.Output);
    }

    public async Task<GitFileDiff> ReadDiffAsync(string localPath, string path, string area, CancellationToken cancellationToken = default)
    {
        if (area is not ("staged" or "working")) throw new ValidationAppException(LocalizedText.Get("changes.invalid_selection"));
        await Slots.WaitAsync(cancellationToken);
        try
        {
            var snapshot = await ReadCoreAsync(localPath, cancellationToken);
            var file = snapshot.Files.FirstOrDefault(x => string.Equals(x.Path, path, StringComparison.Ordinal));
            if (file == null || (area == "staged" ? file.IndexStatus == "." || file.IsConflicted : file.WorkTreeStatus == "."))
                throw new ConflictAppException(LocalizedText.Get("changes.stale"));
            var full = ValidatePath(localPath, file.Path);
            if (file.IsSubmodule) return new(path, area, "submodule", "");
            if (file.IsConflicted) return new(path, area, "conflict", "");
            if (file.IsUntracked) return await PreviewUntrackedAsync(full, path, area, cancellationToken);
            var paths = new List<string> { path };
            if (file.OriginalPath != null) { ValidatePath(localPath, file.OriginalPath); paths.Add(file.OriginalPath); }
            var arguments = new List<string> { "--literal-pathspecs", "-c", "core.fsmonitor=false", "diff", "--no-ext-diff", "--no-textconv", "--no-color", "--no-relative", "--no-exit-code", "--no-quiet", "--submodule=short", "--find-renames" };
            if (area == "staged") arguments.Add("--cached");
            var numbers = await runner.RunLimitedAsync(localPath, [.. arguments, "--numstat", "-z", "--", .. paths], MaximumDiffCharacters, cancellationToken);
            if (numbers.ExitCode != 0) throw new OperationFailedAppException(LocalizedText.Get("changes.diff_failed"));
            if (numbers.Output.StartsWith("-\t-\t", StringComparison.Ordinal)) return new(path, area, "binary", "");
            var result = await runner.RunLimitedAsync(localPath, [.. arguments, "--patch", "--unified=3", "--", .. paths], MaximumDiffCharacters, cancellationToken);
            if (result.ExitCode != 0) throw new OperationFailedAppException(LocalizedText.Get("changes.diff_failed"));
            if (result.Truncated || result.Output.Count(c => c == '\n') > 4000) return new(path, area, "tooLarge", "");
            return new(path, area, result.Output.Length == 0 ? "empty" : "text", result.Output);
        }
        finally { Slots.Release(); }
    }

    private static string ValidatePath(string root, string path)
    {
        if (string.IsNullOrEmpty(path) || Path.IsPathRooted(path) || path.Contains(':') || path.Contains('\0')) throw InvalidPath();
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var full = Path.GetFullPath(Path.Combine(normalizedRoot, path));
        if (!full.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw InvalidPath();
        // Reject links/junctions and Git metadata before reading a working file.
        var current = normalizedRoot;
        foreach (var part in Path.GetRelativePath(normalizedRoot, full).Split(Path.DirectorySeparatorChar))
        {
            if (part.Equals(".git", StringComparison.OrdinalIgnoreCase)) throw InvalidPath();
            current = Path.Combine(current, part);
            if (Path.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw InvalidPath();
        }
        return full;
    }

    private static ValidationAppException InvalidPath() => new(LocalizedText.Get("changes.invalid_selection"));

    private static async Task<GitFileDiff> PreviewUntrackedAsync(string fullPath, string path, string area, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, true);
            if (stream.Length > MaximumDiffCharacters) return new(path, area, "tooLarge", "");
            var bytes = new byte[(int)stream.Length];
            await stream.ReadExactlyAsync(bytes, cancellationToken);
            if (bytes.Contains((byte)0)) return new(path, area, "binary", "");
            var text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
            if (text.Length == 0) return new(path, area, "empty", "");
            var lines = text.Replace("\r\n", "\n").Split('\n');
            if (lines.Length > 4000) return new(path, area, "tooLarge", "");
            // Untracked files have no index version; display a bounded preview as additions.
            var patch = string.Join('\n', lines.Select(line => "+" + line));
            return new(path, area, "text", patch);
        }
        catch (DecoderFallbackException) { return new(path, area, "binary", ""); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { throw new ConflictAppException(LocalizedText.Get("changes.stale")); }
    }
}
