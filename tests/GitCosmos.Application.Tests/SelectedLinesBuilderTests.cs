using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Infrastructure.Git;
namespace GitCosmos.Application.Tests;
public class SelectedLinesBuilderTests
{
    private const string Patch = "diff --git a/file b/file\n--- a/file\n+++ b/file\n@@ -1,3 +1,4 @@\n first\n-old\n+new\n+extra\n last\n";
    [Theory]
    [InlineData(false, "first\nold\nlast\n", "first\nlast\n", 5)]
    [InlineData(false, "first\nold\nlast\n", "first\nold\nnew\nlast\n", 6)]
    [InlineData(false, "first\nold\nlast\n", "first\nold\nextra\nlast\n", 7)]
    [InlineData(true, "first\nnew\nextra\nlast\n", "first\nold\nnew\nextra\nlast\n", 5)]
    [InlineData(true, "first\nnew\nextra\nlast\n", "first\nextra\nlast\n", 6)]
    [InlineData(true, "first\nnew\nextra\nlast\n", "first\nnew\nlast\n", 7)]
    public void AppliesEachIndividualChangeInEitherDirection(bool reverse, string baseline, string expected, int line) => Assert.Equal(expected, SelectedLinesBuilder.Build(baseline, Patch, [line], reverse));
    [Fact]
    public void AppliesReplacementAndAllChangesWithoutLosingTheRestOfTheFile()
    {
        Assert.Equal("first\nnew\nlast\n", SelectedLinesBuilder.Build("first\nold\nlast\n", Patch, [5, 6], false));
        Assert.Equal("first\nold\nlast\n", SelectedLinesBuilder.Build("first\nnew\nextra\nlast\n", Patch, [5, 6, 7], true));
        var patch = "@@ -2 +2 @@\n-b\n+B\n@@ -5 +5 @@\n-e\n+E\n";
        Assert.Equal("a\nb\nc\nd\nE\nf\n", SelectedLinesBuilder.Build("a\nb\nc\nd\ne\nf\n", patch, [4, 5], false));
        Assert.Equal("a\nB\nc\nd\ne\nf\n", SelectedLinesBuilder.Build("a\nB\nc\nd\nE\nf\n", patch, [4, 5], true));
    }
    [Theory]
    [InlineData("one\ntwo\n")]
    [InlineData("one\r\ntwo\r\n")]
    [InlineData("\ufeffcafé\n終わり")]
    [InlineData("last")]
    public void PreservesUnicodeBomCrLfAndMissingFinalNewline(string text)
    {
        var patch = SelectedLinesBuilder.NewFilePatch(text);
        var ids = patch.Split('\n').Select((row, id) => (row, id)).Where(x => x.row.StartsWith('+') && !x.row.StartsWith("+++ ")).Select(x => x.id).ToArray();
        Assert.Equal(text, SelectedLinesBuilder.Build("", patch, ids, false));
        Assert.Equal("", SelectedLinesBuilder.Build(text, patch, ids, true));
    }
    [Fact]
    public void SupportsCreationDeletionInsertionAndUnselectedHunks()
    {
        var created = SelectedLinesBuilder.NewFilePatch("a\nb\n");
        Assert.Equal("b\n", SelectedLinesBuilder.Build("", created, [6], false));
        Assert.Equal("b\n", SelectedLinesBuilder.Build("a\nb\n", created, [5], true));
        const string deleted = "--- a/file\n+++ /dev/null\n@@ -1,2 +0,0 @@\n-a\n-b\n";
        Assert.Equal("b\n", SelectedLinesBuilder.Build("a\nb\n", deleted, [3], false));
        Assert.Equal("a\n", SelectedLinesBuilder.Build("", deleted, [3], true));
        const string insert = "@@ -1,0 +2 @@\n+new\n";
        Assert.Equal("a\nnew\nb\n", SelectedLinesBuilder.Build("a\nb\n", insert, [1], false));
        Assert.Equal("a\nb\n", SelectedLinesBuilder.Build("a\nnew\nb\n", insert, [1], true));
        Assert.DoesNotContain('+', SelectedLinesBuilder.NewFilePatch("").Split('\n').Last());
    }
    [Theory]
    [InlineData(-1)] [InlineData(0)] [InlineData(3)] [InlineData(4)] [InlineData(100)]
    public void RejectsHeadersContextsAndUnknownIds(int id) => Assert.Throws<ValidationAppException>(() => SelectedLinesBuilder.Build("first\nold\nlast\n", Patch, [id], false));
    [Fact]
    public void RejectsEmptyDuplicateMismatchedOverlappingAndMalformedInput()
    {
        Assert.Throws<ValidationAppException>(() => SelectedLinesBuilder.Build("", Patch, [], false));
        Assert.Throws<ValidationAppException>(() => SelectedLinesBuilder.Build("", Patch, [5, 5], false));
        Assert.Throws<ValidationAppException>(() => SelectedLinesBuilder.Build("wrong\n", Patch, [5], false));
        foreach (var patch in new[] { "@@ nope\n+a\n", "@@ -9 +1 @@\n-a\n+b\n", "@@ -1 +1 @@\n-a\n", "@@ -1 +1 @@\n?bad\n", "@@ -1 +1 @@\n\n", "@@ -1 +1 @@\n\\ No newline at end of file\n", "@@ -1 +1 @@\n-a\n\\ No newline at end of file\n\\ No newline at end of file\n+b\n", "@@ -1 +1 @@\n-a\n+b\ndiff --git a/x b/x\n", "@@ -1 +1 @@\n-a\n+b\n@@ -1 +1 @@\n-a\n+c\n" })
            Assert.Throws<ValidationAppException>(() => SelectedLinesBuilder.Build("a\n", patch, [1], false));
        Assert.Throws<ValidationAppException>(() => SelectedLinesBuilder.Build("", "@@ -0,0 +1,2 @@\n+a\n\\ No newline at end of file\n+b\n", [1, 3], false));
        Assert.Throws<ValidationAppException>(() => SelectedLinesBuilder.Build("a\n", "@@ -1,2 +1 @@\n-a\n-b\n+c\n", [1], false));
    }
}
