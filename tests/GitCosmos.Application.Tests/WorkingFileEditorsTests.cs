using System.ComponentModel;
using System.Diagnostics;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Infrastructure.FileSystem;
namespace GitCosmos.Application.Tests;
public class WorkingFileEditorsTests
{
    private sealed class Desktop(string root) : WorkingFileEditors
    {
        public bool Enabled = true, Missing, NullProcess; public Exception? Failure; public ProcessStartInfo? Last;
        protected override bool Windows => Enabled;
        protected override IEnumerable<string> InstallationRoots => [root, ""];
        protected override string ToolboxRoot => Path.Combine(root, "Toolbox");
        protected override bool Exists(string path) => !Missing;
        protected override Process? Start(ProcessStartInfo info) { Last = info; if (Failure is not null) throw Failure; return NullProcess ? null : new Process(); }
    }
    [Fact]
    public void DiscoversInstalledEditorsByTypeAndUsesTheWindowsOpenWithDialog()
    {
        var root = Directory.CreateTempSubdirectory("GitCosmos-editors-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "JetBrains", "Rider 2026", "bin")); Directory.CreateDirectory(Path.Combine(root, "Toolbox", "Rider", "channel", "version", "bin"));
            var file = Path.Combine(root, "file with spaces.cs"); File.WriteAllText(file, "code"); var desktop = new Desktop(root);
            var tools = desktop.Tools(file); Assert.Contains(tools, tool => tool.Id == "rider"); Assert.Contains(tools, tool => tool.Id == "visualstudio"); Assert.DoesNotContain(desktop.Tools("file.vue"), tool => tool.Id == "visualstudio"); Assert.Empty(desktop.Tools("image.png"));
            foreach (var tool in new[] { "vscode", "rider", "visualstudio", "notepadplusplus", "sublime" }) { desktop.RevealLine(file, tool, 17); Assert.False(desktop.Last!.UseShellExecute); Assert.Contains(desktop.Last.ArgumentList, argument => argument.Contains("17", StringComparison.Ordinal)); Assert.Contains(desktop.Last.ArgumentList, argument => argument.Contains(file, StringComparison.Ordinal)); }
            desktop.OpenWith(file); Assert.EndsWith("rundll32.exe", desktop.Last!.FileName); Assert.Equal(new[] { "shell32.dll,OpenAs_RunDLL", file }, desktop.Last.ArgumentList);
            desktop.Missing = true; Assert.Empty(desktop.Tools(file)); Assert.Throws<OperationFailedAppException>(() => desktop.RevealLine(file, "rider", 1)); desktop.Missing = false;
            desktop.Enabled = false; Assert.Empty(desktop.Tools(file)); Assert.Throws<OperationFailedAppException>(() => desktop.OpenWith(file)); desktop.Enabled = true;
            Assert.Throws<OperationFailedAppException>(() => desktop.OpenWith(file + "missing")); Assert.Throws<OperationFailedAppException>(() => desktop.RevealLine(file, "invalid", 1)); Assert.Throws<OperationFailedAppException>(() => desktop.RevealLine(file, "vscode", 0));
            desktop.NullProcess = true; Assert.Throws<OperationFailedAppException>(() => desktop.OpenWith(file)); Assert.Throws<OperationFailedAppException>(() => desktop.RevealLine(file, "vscode", 1)); desktop.NullProcess = false;
            foreach (var failure in new Exception[] { new Win32Exception(), new IOException(), new UnauthorizedAccessException() }) { desktop.Failure = failure; Assert.Throws<OperationFailedAppException>(() => desktop.OpenWith(file)); Assert.Throws<OperationFailedAppException>(() => desktop.RevealLine(file, "vscode", 1)); }
        }
        finally { Directory.Delete(root, true); }
    }
}
