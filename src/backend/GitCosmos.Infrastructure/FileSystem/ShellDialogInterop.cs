using System.Runtime.InteropServices;

namespace GitCosmos.Infrastructure.FileSystem;

// Native vtable order is significant. Unused members retain their SDK slots.
[ComImport, Guid("42F85136-DB7E-439C-85F1-E4075D135FC8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IFileDialog
{
    [PreserveSig] int Show(nint owner);
    void SetFileTypes(uint count, nint filters);
    void SetFileTypeIndex(uint index);
    void GetFileTypeIndex(out uint index);
    void Advise(nint events, out uint cookie);
    void Unadvise(uint cookie);
    void SetOptions(uint options);
    void GetOptions(out uint options);
    void SetDefaultFolder(IShellItem item);
    void SetFolder(IShellItem item);
    [return: MarshalAs(UnmanagedType.Interface)] IShellItem GetFolder();
    [return: MarshalAs(UnmanagedType.Interface)] IShellItem GetCurrentSelection();
    void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
    [return: MarshalAs(UnmanagedType.LPWStr)] string GetFileName();
    void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
    void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
    void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
    [return: MarshalAs(UnmanagedType.Interface)] IShellItem GetResult();
    void AddPlace(IShellItem item, uint position);
    void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
    void Close(int status);
    void SetClientGuid(ref Guid guid);
    void ClearClientData();
    void SetFilter(nint filter);
}

[ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IShellItem
{
    void BindToHandler(nint context, ref Guid handler, ref Guid iid, out nint result);
    [return: MarshalAs(UnmanagedType.Interface)] IShellItem GetParent();
    [return: MarshalAs(UnmanagedType.LPWStr)] string GetDisplayName(uint nameType);
    void GetAttributes(uint mask, out uint attributes);
    void Compare(IShellItem other, uint hint, out int order);
}
