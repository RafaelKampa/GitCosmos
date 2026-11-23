using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.Interfaces.Git;

namespace GitCosmos.Infrastructure.Git;

public static class GitStatusParser
{
    public static GitWorkingTreeStatus Parse(string output)
    {
        string? branch = null; bool? hasCommits = null;
        string? upstream = null; int? ahead = null, behind = null;
        int changed = 0, modified = 0, added = 0, deleted = 0, renamed = 0, untracked = 0, conflicted = 0, staged = 0, unstaged = 0;
        var entries = output.Split('\0');
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (entry.Length == 0) continue;
            if (entry.StartsWith("# branch.head ", StringComparison.Ordinal)) { branch = entry[14..]; continue; }
            if (entry.StartsWith("# branch.oid ", StringComparison.Ordinal)) { hasCommits = entry[13..] != "(initial)"; continue; }
            if (entry.StartsWith("# branch.upstream ", StringComparison.Ordinal)) { upstream = entry[18..]; continue; }
            if (entry.StartsWith("# branch.ab ", StringComparison.Ordinal))
            {
                var parts = entry[12..].Split(' ');
                if (parts.Length != 2 || !parts[0].StartsWith('+') || !parts[1].StartsWith('-')
                    || !int.TryParse(parts[0].AsSpan(1), out var a) || a < 0 || !int.TryParse(parts[1].AsSpan(1), out var b) || b < 0) throw InvalidOutput();
                ahead = a; behind = b; continue;
            }
            if (entry.StartsWith("# ", StringComparison.Ordinal) || entry.StartsWith("! ", StringComparison.Ordinal)) continue;
            if (entry.StartsWith("? ", StringComparison.Ordinal)) { untracked++; changed++; continue; }
            if (entry.StartsWith("u ", StringComparison.Ordinal)) { conflicted++; changed++; continue; }
            if (entry.Length < 5 || entry[1] != ' ' || entry[4] != ' ' || entry[0] is not ('1' or '2')) throw InvalidOutput();
            var xy = entry.Substring(2, 2);
            if (xy.Contains('M') || xy.Contains('T')) modified++;
            if (xy.Contains('A') || xy.Contains('C')) added++;
            if (xy.Contains('D')) deleted++;
            if (xy.Contains('R')) renamed++;
            if (xy[0] != '.') staged++;
            if (xy[1] != '.') unstaged++;
            changed++;
            // A rename/copy consumes one extra NUL-delimited original path, even if it resembles a record.
            if (entry[0] == '2')
            {
                if (++i >= entries.Length || entries[i].Length == 0) throw InvalidOutput();
            }
        }
        if (string.IsNullOrWhiteSpace(branch) || hasCommits is null) throw InvalidOutput();
        return new(branch, branch == "(detached)", hasCommits.Value, changed, modified, added, deleted, renamed, untracked, conflicted, staged, unstaged) { Upstream = upstream, Ahead = ahead, Behind = behind };
    }

    private static OperationFailedAppException InvalidOutput() => new(LocalizedText.Get("dashboard.invalid_status"));
}
