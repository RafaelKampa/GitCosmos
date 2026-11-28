using System.Text.RegularExpressions;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
namespace GitCosmos.Infrastructure.Git;

/// <summary>Reconstructs the index, retaining every unselected change on its original side.</summary>
public static class SelectedLinesBuilder
{
    private record Line(string Text, bool Newline);
    private record Entry(int Id, char Kind, Line Value);
    private static readonly Regex Header = new(@"^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    public static string Build(string baseline, string patch, IReadOnlyCollection<int> selected, bool reverse)
    {
        var wanted = selected.ToHashSet();
        if (wanted.Count == 0 || wanted.Count != selected.Count) throw Invalid();
        var source = Split(baseline); var output = new List<Line>(); var position = 0;
        var records = patch.Split('\n'); var found = new HashSet<int>(); var hadHunk = false;
        for (var i = 0; i < records.Length; i++)
        {
            if (!records[i].StartsWith("@@", StringComparison.Ordinal))
            {
                if (hadHunk && records[i].Length != 0) throw Invalid();
                continue;
            }
            hadHunk = true; var match = Header.Match(records[i]); if (!match.Success) throw Invalid();
            int Number(int group, int fallback) => match.Groups[group].Success && int.TryParse(match.Groups[group].Value, out var value) ? value : fallback;
            var oldCount = Number(2, 1); var newCount = Number(4, 1);
            var count = reverse ? newCount : oldCount; var start = Number(reverse ? 3 : 1, -1);
            var offset = count == 0 ? start : start - 1;
            if (offset < position || offset > source.Count) throw Invalid();
            output.AddRange(source.GetRange(position, offset - position)); position = offset;
            var entries = new List<Entry>(); var oldSeen = 0; var newSeen = 0;
            while (i + 1 < records.Length && !records[i + 1].StartsWith("@@", StringComparison.Ordinal))
            {
                var row = records[i + 1];
                if (row.Length == 0 && i + 1 == records.Length - 1) { i++; break; }
                i++;
                if (row == "\\ No newline at end of file")
                {
                    if (entries.Count == 0 || !entries[^1].Value.Newline) throw Invalid();
                    entries[^1] = entries[^1] with { Value = entries[^1].Value with { Newline = false } }; continue;
                }
                if (row.Length == 0 || row[0] is not (' ' or '+' or '-')) throw Invalid();
                entries.Add(new(i, row[0], new(row[1..], true)));
                if (row[0] != '+') oldSeen++; if (row[0] != '-') newSeen++;
            }
            if (oldSeen != oldCount || newSeen != newCount) throw Invalid();
            foreach (var entry in entries)
            {
                var chosen = wanted.Contains(entry.Id);
                if (chosen) { if (entry.Kind == ' ') throw Invalid(); found.Add(entry.Id); }
                var consumes = entry.Kind == ' ' || entry.Kind == (reverse ? '+' : '-');
                if (consumes)
                {
                    if (position >= source.Count || source[position] != entry.Value) throw Invalid();
                    if (entry.Kind == ' ' || !chosen) output.Add(source[position]);
                    position++;
                }
                else if (chosen) output.Add(entry.Value);
            }
        }
        if (!found.SetEquals(wanted)) throw Invalid();
        output.AddRange(source.Skip(position));
        if (output.Take(Math.Max(0, output.Count - 1)).Any(line => !line.Newline)) throw Invalid();
        return string.Concat(output.Select(line => line.Text + (line.Newline ? "\n" : "")));
    }
    public static string NewFilePatch(string text)
    {
        var lines = Split(text);
        return "diff --git a/file b/file\nnew file mode 100644\n--- /dev/null\n+++ b/file\n@@ -0,0 +1," + lines.Count + " @@\n" +
            string.Concat(lines.Select(line => "+" + line.Text + "\n" + (line.Newline ? "" : "\\ No newline at end of file\n")));
    }
    public static int[] ChangedLineIds(string patch)
    {
        var inHunk = false; var result = new List<int>(); var rows = patch.Split('\n');
        for (var id = 0; id < rows.Length; id++)
        {
            if (rows[id].StartsWith("@@ ", StringComparison.Ordinal)) inHunk = true;
            else if (inHunk && rows[id].Length > 0 && rows[id][0] is '+' or '-') result.Add(id);
        }
        return result.ToArray();
    }
    private static List<Line> Split(string text)
    {
        var result = new List<Line>(); var start = 0;
        for (var i = 0; i < text.Length; i++) if (text[i] == '\n') { result.Add(new(text[start..i], true)); start = i + 1; }
        if (start < text.Length) result.Add(new(text[start..], false));
        return result;
    }
    private static ValidationAppException Invalid() => new(LocalizedText.Get("lines.invalid"));
}
