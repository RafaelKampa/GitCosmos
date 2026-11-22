using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.Interfaces.Git;

namespace GitCosmos.Infrastructure.Git;

public static class GitChangesParser
{
    public static GitChangesSnapshot Parse(string output)
    {
        var summary = GitStatusParser.Parse(output);
        var files = new List<GitFileChange>();
        var entries = output.Split('\0');
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (entry.Length == 0 || entry.StartsWith("# ", StringComparison.Ordinal) || entry.StartsWith("! ", StringComparison.Ordinal)) continue;
            if (entry.StartsWith("? ", StringComparison.Ordinal))
            {
                files.Add(new(entry[2..], null, ".", "?", true, false, false));
                continue;
            }
            var count = entry[0] switch { '1' => 9, '2' => 10, 'u' => 11, _ => 0 };
            var fields = entry.Split(' ', count);
            if (count == 0 || fields.Length != count || fields[1].Length != 2 || fields[^1].Length == 0) throw InvalidOutput();
            string? original = null;
            if (entry[0] == '2')
            {
                if (++i >= entries.Length || entries[i].Length == 0) throw InvalidOutput();
                original = entries[i];
            }
            files.Add(new(fields[^1], original, fields[1][0].ToString(), fields[1][1].ToString(), false, entry[0] == 'u', fields[2].StartsWith('S')));
        }
        return new(summary, files);
    }

    private static OperationFailedAppException InvalidOutput() => new(LocalizedText.Get("dashboard.invalid_status"));
}
