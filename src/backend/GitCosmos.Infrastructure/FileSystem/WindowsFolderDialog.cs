using System.Runtime.InteropServices;
using System.Text;

namespace GitCosmos.Infrastructure.FileSystem;

public sealed class WindowsFolderDialog(IFileDialog dialog, Func<string, IShellItem> createShellItem) : IWindowsFolderDialog
{
    public const int CancelledHResult = unchecked((int)0x800704C7);
    public const uint FolderOptions = 0x20 | 0x40 | 0x800 | 0x8; // PICKFOLDERS, FORCEFILESYSTEM, PATHMUSTEXIST, NOCHANGEDIR
    public const uint FileSystemPath = 0x80058000;
    private uint _threadId;

    public string? Show(string title, string confirmLabel, string? initialPath)
    {
        _threadId = GetCurrentThreadId();
        dialog.GetOptions(out var options);
        dialog.SetOptions(options | FolderOptions);
        dialog.SetTitle(title);
        dialog.SetOkButtonLabel(confirmLabel);
        var clientId = new Guid("ABFAD584-EBB8-4DD7-B815-CC233B970889");
        dialog.SetClientGuid(ref clientId);
        if (!string.IsNullOrWhiteSpace(initialPath) && Path.IsPathFullyQualified(initialPath) && Directory.Exists(initialPath))
        {
            var initial = createShellItem(Path.GetFullPath(initialPath));
            try { dialog.SetFolder(initial); }
            finally { Release(initial); }
        }
        var status = dialog.Show(GetForegroundWindow());
        if (status == CancelledHResult) return null;
        Marshal.ThrowExceptionForHR(status);
        var selected = dialog.GetResult();
        try { return selected.GetDisplayName(FileSystemPath); }
        finally { Release(selected); }
    }

    public void Close()
    {
        var threadId = Volatile.Read(ref _threadId);
        if (threadId == 0) return;
        EnumThreadWindows(threadId, (window, _) =>
        {
            var name = new StringBuilder(64);
            GetClassName(window, name, name.Capacity);
            if (name.ToString() == "#32770") PostMessage(window, 0x0010, nint.Zero, nint.Zero);
            return true;
        }, nint.Zero);
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _threadId, 0);
        Release(dialog);
    }

    private static void Release(object instance)
    {
        if (OperatingSystem.IsWindows() && Marshal.IsComObject(instance)) Marshal.FinalReleaseComObject(instance);
    }

    private delegate bool EnumWindowsCallback(nint window, nint state);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool EnumThreadWindows(uint threadId, EnumWindowsCallback callback, nint state);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, StringBuilder name, int capacity);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool PostMessage(nint window, uint message, nint wParam, nint lParam);
}
