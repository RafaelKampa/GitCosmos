using System.Text;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.Interfaces.Git;
namespace GitCosmos.Infrastructure.Git;
internal static class SelectedPatchExport
{
    public static Task<string> Build(IBoundedGitProcessRunner reader, string root, string path, string before, string after, bool newFile, bool removed, CancellationToken ct)
        => BuildBytes(reader, root, path, Encoding.UTF8.GetBytes(before), Encoding.UTF8.GetBytes(after), newFile, removed, ct, 200000);
    public static async Task<string> BuildBytes(IBoundedGitProcessRunner reader, string root, string path, byte[] before, byte[] after, bool newFile, bool removed, CancellationToken ct, int limit = 2_000_000)
    {
        var directory = Directory.CreateTempSubdirectory("GitCosmos-Patch-").FullName;
        var first = Path.Combine(directory, "before"); var second = Path.Combine(directory, "after");
        try
        {
            await File.WriteAllBytesAsync(first, before, ct); await File.WriteAllBytesAsync(second, after, ct);
            var output = await reader.RunLimitedAsync(root, ["diff", "--no-index", "--patch", "--binary", "--full-index", "--no-ext-diff", "--no-textconv", "--no-color", "--no-renames", "--unified=3", "--output-indicator-new=+", "--output-indicator-old=-", "--output-indicator-context= ", "--", first, second], limit, ct);
            if (output.ExitCode is not (0 or 1) || output.Truncated) throw Failed();
            var a = LocalFiles.QuotePath("a/" + path); var b = LocalFiles.QuotePath("b/" + path);
            var header = "diff --git " + a + " " + b + "\n" + (newFile ? "new file mode 100644\n" : removed ? "deleted file mode 100644\n" : "");
            if (output.Output.Length == 0)
            {
                if (!newFile && !removed) return "";
                var hash = await reader.RunLimitedAsync(root, ["hash-object", "--no-filters", "--", first], 200, ct);
                var oid = hash.Output.Trim(); if (hash.ExitCode != 0 || hash.Truncated || !GitHistoryParser.IsSha(oid)) throw Failed();
                var zero = new string('0', oid.Length);
                return header + "index " + (newFile ? zero + ".." + oid : oid + ".." + zero) + "\n";
            }
            var binary = output.Output.IndexOf("\nGIT binary patch\n", StringComparison.Ordinal);
            if (binary >= 0)
            {
                var index = output.Output.Split('\n').FirstOrDefault(row => row.StartsWith("index ", StringComparison.Ordinal)) ?? throw Invalid();
                var ids = index.Split(' ')[1].Split("..", StringSplitOptions.None);
                if (ids.Length != 2 || !ids.All(GitHistoryParser.IsSha)) throw Invalid();
                return header + "index " + (newFile ? new string('0', ids[0].Length) : ids[0]) + ".." + (removed ? new string('0', ids[1].Length) : ids[1]) + (newFile || removed ? "" : " 100644") + output.Output[binary..];
            }
            var hunk = output.Output.IndexOf("\n@@ ", StringComparison.Ordinal); if (hunk < 0) throw Invalid();
            return header + "--- " + (newFile ? "/dev/null" : a) + "\n+++ " + (removed ? "/dev/null" : b) + output.Output[hunk..];
        }
        finally { if (File.Exists(first)) File.Delete(first); if (File.Exists(second)) File.Delete(second); Directory.Delete(directory); }
    }
    private static OperationFailedAppException Failed() => new(LocalizedText.Get("local.failed"));
    private static ValidationAppException Invalid() => new(LocalizedText.Get("lines.invalid"));
}
