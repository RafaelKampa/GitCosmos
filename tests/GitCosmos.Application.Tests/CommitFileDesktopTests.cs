using System.ComponentModel;
using System.Diagnostics;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Infrastructure.FileSystem;
namespace GitCosmos.Application.Tests;
public class CommitFileDesktopTests
{
    private sealed class Desktop : CommitFileDesktop, IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("GitCosmos-desktop-tests-").FullName;
        public bool Supported = true, Code = true, Studio = true, NullProcess;
        public Exception? Error;
        public List<ProcessStartInfo> Calls { get; } = [];
        protected override bool SupportsDesktop => Supported;
        protected override bool Exists(string path) => path.EndsWith("Code.exe") ? Code : Studio;
        protected override string CreateExportDirectory() => Directory.CreateDirectory(Path.Combine(Root, Guid.NewGuid().ToString("N"))).FullName;
        protected override Process? Start(ProcessStartInfo info) { Calls.Add(info); if (Error is not null) throw Error; return NullProcess ? null : new Process(); }
        public void Dispose() => Directory.Delete(Root, true);
    }
    [Fact]
    public void DetectsToolsAndOpensDocumentsAndDiffsWithoutStartingRealPrograms()
    {
        using var desktop = new Desktop(); Assert.Equal(2, desktop.Tools().Count);
        desktop.Open("file.cs", [1, 2]); var open = desktop.Calls.Last();
        Assert.Contains("--disable-extensions", open.ArgumentList); Assert.Equal(new byte[] { 1, 2 }, File.ReadAllBytes(open.ArgumentList.Last())); Assert.False(open.UseShellExecute);
        desktop.Diff("vscode", "file.cs", [1], [2]); Assert.Contains("--diff", desktop.Calls.Last().ArgumentList);
        desktop.Diff("visualstudio", "file.cs", [1], [2]); Assert.Contains("/diff", desktop.Calls.Last().ArgumentList);
        desktop.Code = false; desktop.Studio = false; Assert.Empty(desktop.Tools());
        desktop.Open("script.cmd", [3]); Assert.EndsWith("notepad.exe", desktop.Calls.Last().FileName);
        Assert.Throws<OperationFailedAppException>(() => desktop.Diff("arbitrary", "file", [], []));
        var full = Path.Combine(desktop.Root, "test.txt"); File.WriteAllText(full, "content");
        desktop.Code = true; desktop.Edit(full); Assert.Contains("--disable-extensions", desktop.Calls.Last().ArgumentList);
        desktop.Code = false; desktop.Edit(full); Assert.EndsWith("notepad.exe", desktop.Calls.Last().FileName);
        Assert.Throws<OperationFailedAppException>(() => desktop.Edit(Path.Combine(desktop.Root, "missing.txt")));
        desktop.Reveal(full); Assert.Equal("/select,", desktop.Calls.Last().ArgumentList[0]);
        desktop.Reveal(Path.Combine(desktop.Root, "missing", "file")); Assert.Equal(desktop.Root, desktop.Calls.Last().ArgumentList[0]);
        desktop.Supported = false; Assert.Empty(desktop.Tools()); Assert.Throws<OperationFailedAppException>(() => desktop.Open("file", [])); Assert.Throws<OperationFailedAppException>(() => desktop.Reveal(full));
        Assert.Throws<OperationFailedAppException>(() => desktop.Edit(full));
    }
    [Theory]
    [InlineData("native")]
    [InlineData("io")]
    [InlineData("access")]
    [InlineData("null")]
    public void HidesNativeDiagnosticsAndRejectsInvalidExportNames(string failure)
    {
        using var desktop = new Desktop(); desktop.Error = failure switch { "native" => new Win32Exception("private"), "io" => new IOException("private"), "access" => new UnauthorizedAccessException("private"), _ => null }; desktop.NullProcess = failure == "null";
        var error = Assert.Throws<OperationFailedAppException>(() => desktop.Open("file.txt", [])); Assert.DoesNotContain("private", error.Message);
        Assert.Throws<OperationFailedAppException>(() => desktop.Open("", []));
        if (OperatingSystem.IsWindows()) Assert.Throws<OperationFailedAppException>(() => desktop.Open("bad:name", []));
    }
    [Fact]
    public void ActualDiscoveryOnlyReadsInstallationLocations()
    {
        Assert.All(new CommitFileDesktop().Tools(), tool => Assert.Contains(tool.Id, new[] { "vscode", "visualstudio" }));
    }
}
