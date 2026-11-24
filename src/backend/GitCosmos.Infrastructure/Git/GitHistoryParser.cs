using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.Interfaces.Git;

namespace GitCosmos.Infrastructure.Git;

public static class GitHistoryParser
{
    // NUL separators preserve tabs, newlines, Unicode and spaces in Git metadata.
    public const string Format = "%H%x00%P%x00%an%x00%ae%x00%aI%x00%s%x00%D%x00%B";

    public static IReadOnlyList<GitCommit> ParseCommits(string output)
    {
        if (output.Length == 0) return [];
        var fields = output.Split('\0');
        if (fields[^1] != "" || (fields.Length - 1) % 8 != 0) throw Invalid();
        var commits = new List<GitCommit>();
        for (var i = 0; i < fields.Length - 1; i += 8)
        {
            var parents = fields[i + 1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (!IsSha(fields[i]) || parents.Any(x => !IsSha(x)) || !DateTimeOffset.TryParse(fields[i + 4], out _)) throw Invalid();
            commits.Add(new(fields[i], parents, fields[i + 2], fields[i + 3], fields[i + 4], fields[i + 5], fields[i + 6], fields[i + 7]));
        }
        return commits;
    }

    public static IReadOnlyList<GitCommitFile> ParseFiles(string output)
    {
        if (output.Length == 0) return [];
        var fields = output.Split('\0');
        if (fields[^1] != "") throw Invalid();
        var files = new List<GitCommitFile>();
        for (var i = 0; i < fields.Length - 1;)
        {
            var status = fields[i++];
            if (status.Length == 0 || !"ACDMRTUXB".Contains(status[0]) || i >= fields.Length - 1) throw Invalid();
            var path = fields[i++];
            string? original = null;
            if (status[0] is 'R' or 'C')
            {
                if (i >= fields.Length - 1) throw Invalid();
                original = path; path = fields[i++];
            }
            if (path.Length == 0) throw Invalid();
            files.Add(new(path, original, status));
        }
        return files;
    }

    public static bool IsSha(string value) => value.Length is 40 or 64 && value.All(Uri.IsHexDigit);
    private static OperationFailedAppException Invalid() => new(LocalizedText.Get("history.failed"));
}
