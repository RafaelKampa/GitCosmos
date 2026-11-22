using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace GitCosmos.Infrastructure.FileSystem;

public class WindowsFolderDialogFactory : IWindowsFolderDialogFactory
{
    public bool IsSupported => OperatingSystem.IsWindows() && Environment.UserInteractive;

    public IWindowsFolderDialog Create()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        var type = Type.GetTypeFromCLSID(new Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7"))!;
        var dialog = (IFileDialog)Activator.CreateInstance(type)!;
        return new WindowsFolderDialog(dialog, CreateShellItem);
    }

    [SupportedOSPlatform("windows")]
    public static IShellItem CreateShellItem(string path)
    {
        var iid = typeof(IShellItem).GUID;
        Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(path, nint.Zero, ref iid, out var item));
        return item;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(string path, nint bindingContext, ref Guid iid, out IShellItem item);
}
