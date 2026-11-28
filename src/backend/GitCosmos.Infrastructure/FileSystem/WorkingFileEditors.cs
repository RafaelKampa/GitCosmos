using System.ComponentModel;
using System.Diagnostics;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.Interfaces.Git;
namespace GitCosmos.Infrastructure.FileSystem;
public class WorkingFileEditors : IWorkingFileEditors
{
    protected virtual bool Windows => OperatingSystem.IsWindows();
    protected virtual bool Exists(string path) => File.Exists(path);
    protected virtual Process? Start(ProcessStartInfo info) => Process.Start(info);
    protected virtual IEnumerable<string> InstallationRoots => [Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs")];
    protected virtual string ToolboxRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JetBrains", "Toolbox", "apps");
    protected virtual IEnumerable<(string Id, string Name, string Path)> Candidates()
    {
        foreach (var root in InstallationRoots.Where(Path.IsPathFullyQualified))
        {
            yield return ("vscode", "VS Code", Path.Combine(root, "Microsoft VS Code", "Code.exe"));
            yield return ("notepadplusplus", "Notepad++", Path.Combine(root, "Notepad++", "notepad++.exe"));
            yield return ("sublime", "Sublime Text", Path.Combine(root, "Sublime Text", "sublime_text.exe"));
            foreach (var year in new[] { "2022", "2026", "18" }) foreach (var edition in new[] { "Community", "Professional", "Enterprise" }) yield return ("visualstudio", "Visual Studio", Path.Combine(root, "Microsoft Visual Studio", year, edition, "Common7", "IDE", "devenv.exe"));
            foreach (var directory in Directories(Path.Combine(root, "JetBrains"))) if (Path.GetFileName(directory).Contains("Rider", StringComparison.OrdinalIgnoreCase)) yield return ("rider", "Rider", Path.Combine(directory, "bin", "rider64.exe"));
        }
        var toolbox = ToolboxRoot;
        foreach (var app in Directories(toolbox).Where(path => Path.GetFileName(path).Contains("Rider", StringComparison.OrdinalIgnoreCase)))
        {
            yield return ("rider", "Rider", Path.Combine(app, "bin", "rider64.exe"));
            foreach (var channel in Directories(app)) foreach (var version in Directories(channel)) yield return ("rider", "Rider", Path.Combine(version, "bin", "rider64.exe"));
        }
    }
    private static string[] Directories(string path)
    {
        try { return Directory.Exists(path) ? Directory.EnumerateDirectories(path).Where(folder => !File.GetAttributes(folder).HasFlag(FileAttributes.ReparsePoint)).Take(100).ToArray() : []; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return []; }
    }
    private Dictionary<string, (string Name, string Path)> Discover(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        var code = new[] { ".cs", ".csx", ".csproj", ".sln", ".slnx", ".razor", ".vb", ".fs", ".vue", ".ts", ".tsx", ".js", ".jsx", ".json", ".xml", ".html", ".css", ".scss", ".md", ".txt", ".yaml", ".yml", ".config", ".props", ".targets", ".ps1", ".sql", ".py", ".java", ".go", ".rs", ".c", ".cpp", ".h", "" };
        var result = new Dictionary<string, (string, string)>(); if (!Windows || !code.Contains(extension)) return result;
        foreach (var candidate in Candidates())
        {
            if (candidate.Id == "visualstudio" && extension is ".vue" or ".md" or ".py" or ".go" or ".rs") continue;
            if (Exists(candidate.Path)) result.TryAdd(candidate.Id, (candidate.Name, candidate.Path));
        }
        return result;
    }
    public IReadOnlyList<CommitExternalTool> Tools(string path) => Discover(path).Select(pair => new CommitExternalTool(pair.Key, pair.Value.Name)).ToArray();
    public void OpenWith(string path)
    {
        if (!Windows || !File.Exists(path)) throw Failed();
        var info = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "rundll32.exe")) { UseShellExecute = false, CreateNoWindow = true };
        info.ArgumentList.Add("shell32.dll,OpenAs_RunDLL"); info.ArgumentList.Add(path);
        try { using var process = Start(info) ?? throw Failed(); }
        catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException) { throw Failed(); }
    }
    public void RevealLine(string path, string tool, int line)
    {
        if (line < 1 || !File.Exists(path) || !Discover(path).TryGetValue(tool, out var editor)) throw Failed();
        var info = new ProcessStartInfo(editor.Path) { UseShellExecute = false, CreateNoWindow = true };
        string[] args = tool switch
        {
            "vscode" => ["--new-window", "--disable-extensions", "--goto", path + ":" + line],
            "rider" => ["--line", line.ToString(System.Globalization.CultureInfo.InvariantCulture), path],
            "visualstudio" => ["/Edit", path, "/Command", "Edit.Goto " + line],
            "notepadplusplus" => ["-n" + line, path],
            "sublime" => [path + ":" + line],
            _ => throw Failed()
        };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        try { using var process = Start(info) ?? throw Failed(); }
        catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException) { throw Failed(); }
    }
    private static OperationFailedAppException Failed() => new(LocalizedText.Get("commit.desktop_failed"));
}
