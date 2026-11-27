using System.ComponentModel;
using System.Diagnostics;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.Interfaces.Git;
namespace GitCosmos.Infrastructure.FileSystem;

public class CommitFileDesktop : ICommitFileDesktop
{
    protected virtual bool SupportsDesktop => OperatingSystem.IsWindows();
    protected virtual bool Exists(string path) => File.Exists(path);
    protected virtual Process? Start(ProcessStartInfo info) => Process.Start(info);
    protected virtual string CreateExportDirectory() => Directory.CreateTempSubdirectory("GitCosmos-Commit-").FullName;
    private Dictionary<string, (string Name, string Path)> Discover()
    {
        var tools = new Dictionary<string, (string, string)>();
        if (!SupportsDesktop) return tools;
        var code = new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Microsoft VS Code", "Code.exe"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft VS Code", "Code.exe"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft VS Code", "Code.exe") }.FirstOrDefault(Exists);
        if (code is not null) tools["vscode"] = ("VS Code", code);
        foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
            foreach (var year in new[] { "2022", "2026", "18" })
                foreach (var edition in new[] { "Community", "Professional", "Enterprise" })
                {
                    var exe = Path.Combine(root, "Microsoft Visual Studio", year, edition, "Common7", "IDE", "devenv.exe");
                    if (Exists(exe)) tools.TryAdd("visualstudio", ("Visual Studio", exe));
                }
        return tools;
    }
    public IReadOnlyList<CommitExternalTool> Tools() => Discover().Select(pair => new CommitExternalTool(pair.Key, pair.Value.Name)).ToArray();
    private void Launch(string executable, params string[] args)
    {
        try
        {
            var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
            foreach (var arg in args) info.ArgumentList.Add(arg);
            using var process = Start(info) ?? throw Failed();
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException) { throw Failed(); }
    }
    private static string Export(string directory, string name, byte[] bytes)
    {
        var safeName = Path.GetFileName(name);
        if (safeName.Length == 0 || safeName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw Failed();
        Directory.CreateDirectory(directory); var full = Path.Combine(directory, safeName); File.WriteAllBytes(full, bytes); return full;
    }
    public void Open(string name, byte[] content)
    {
        if (!SupportsDesktop) throw Failed();
        var directory = CreateExportDirectory();
        var path = Export(directory, name, content);
        // Open snapshots as documents, never execute the file from the commit.
        var code = Discover().GetValueOrDefault("vscode").Path;
        if (code is not null) Launch(code, "--new-window", "--disable-extensions", path);
        else Launch(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe"), path);
    }
    public void Diff(string tool, string name, byte[] before, byte[] after)
    {
        if (!Discover().TryGetValue(tool, out var installed)) throw Failed();
        var directory = CreateExportDirectory();
        var oldPath = Export(Path.Combine(directory, "before"), name, before);
        var newPath = Export(Path.Combine(directory, "after"), name, after);
        if (tool == "vscode") Launch(installed.Path, "--new-window", "--disable-extensions", "--diff", oldPath, newPath);
        else Launch(installed.Path, "/diff", oldPath, newPath);
    }
    public void Reveal(string fullPath)
    {
        if (!SupportsDesktop) throw Failed();
        var explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        if (File.Exists(fullPath)) Launch(explorer, "/select,", fullPath);
        else
        {
            var parent = Path.GetDirectoryName(fullPath)!;
            while (!Directory.Exists(parent)) parent = Path.GetDirectoryName(parent) ?? throw Failed();
            Launch(explorer, parent);
        }
    }
    public void Edit(string fullPath)
    {
        if (!SupportsDesktop || !File.Exists(fullPath)) throw Failed();
        var code = Discover().GetValueOrDefault("vscode").Path;
        if (code is not null) Launch(code, "--new-window", "--disable-extensions", fullPath);
        else Launch(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe"), fullPath);
    }
    private static OperationFailedAppException Failed() => new(LocalizedText.Get("commit.desktop_failed"));
}
