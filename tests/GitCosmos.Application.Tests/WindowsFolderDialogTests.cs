using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using GitCosmos.Infrastructure.FileSystem;

namespace GitCosmos.Application.Tests;

// The native Show call is replaced: this suite never opens a folder selection window.
[SupportedOSPlatform("windows")]
public class WindowsFolderDialogTests
{
    [Fact]
    public async Task Dialog_ConfiguresTheExplorerFolderPickerAndReturnsTheFilesystemPath()
    {
        await InSta(() =>
        {
            var directory = Directory.CreateTempSubdirectory("GitCosmos-picker-");
            try
            {
                var item = new FakeShellItem { Path = directory.FullName };
                var com = new FakeFileDialog { Result = item };
                using var dialog = new WindowsFolderDialog(com, _ => item);
                dialog.Close(); // Before Show, no native window belongs to the worker.
                Assert.Equal(directory.FullName, dialog.Show("Localized title", "Localized selection", directory.FullName));
                Assert.Equal("Localized title", com.Title);
                Assert.Equal("Localized selection", com.ConfirmLabel);
                Assert.Equal(WindowsFolderDialog.FolderOptions | 0x1000u, com.Options);
                Assert.Same(item, com.InitialFolder);
                Assert.Equal(WindowsFolderDialog.FileSystemPath, item.NameType);
                Assert.Equal(1, com.ResultReads);
                dialog.Close(); // Enumerates only this STA thread; no real Show was called.
            }
            finally { Directory.Delete(directory.FullName); }
        });
    }

    [Fact]
    public async Task Dialog_CancelDoesNotReadAResultAndInvalidInitialHintsUseTheDefaultFolder()
    {
        await InSta(() =>
        {
            foreach (var initial in new[] { null, " ", "relative", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()) })
            {
                var com = new FakeFileDialog { Status = WindowsFolderDialog.CancelledHResult };
                using var dialog = new WindowsFolderDialog(com, _ => throw new InvalidOperationException());
                Assert.Null(dialog.Show("Title", "Select", initial));
                Assert.Null(com.InitialFolder);
                Assert.Equal(0, com.ResultReads);
            }
            var failed = new FakeFileDialog { Status = unchecked((int)0x80004005) };
            using var broken = new WindowsFolderDialog(failed, _ => throw new InvalidOperationException());
            Assert.Throws<COMException>(() => broken.Show("Title", "Select", null));
        });
    }

    [Fact]
    public async Task NativeFactory_CreatesAndReleasesShellObjectsWithoutShowingAWindow()
    {
        await InSta(() =>
        {
            var factory = new WindowsFolderDialogFactory();
            Assert.Equal(Environment.UserInteractive, factory.IsSupported);
            using var dialog = factory.Create();
            dialog.Close();
            var item = WindowsFolderDialogFactory.CreateShellItem(Path.GetTempPath());
            try
            {
                Assert.True(Directory.Exists(item.GetDisplayName(WindowsFolderDialog.FileSystemPath)));
            }
            finally { Marshal.FinalReleaseComObject(item); }
        });
    }

    private static async Task InSta(Action action)
    {
        if (!OperatingSystem.IsWindows()) return;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { action(); completion.SetResult(); }
            catch (Exception exception) { completion.SetException(exception); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private sealed class FakeFileDialog : IFileDialog
    {
        public uint Options { get; private set; } = 0x1000;
        public string? Title { get; private set; }
        public string? ConfirmLabel { get; private set; }
        public IShellItem? InitialFolder { get; private set; }
        public IShellItem? Result { get; init; }
        public int Status { get; init; }
        public int ResultReads { get; private set; }
        public int Show(nint owner) => Status;
        public void SetOptions(uint options) => Options = options;
        public void GetOptions(out uint options) => options = Options;
        public void SetTitle(string title) => Title = title;
        public void SetOkButtonLabel(string label) => ConfirmLabel = label;
        public void SetFolder(IShellItem item) => InitialFolder = item;
        public IShellItem GetResult() { ResultReads++; return Result!; }
        public void SetClientGuid(ref Guid guid) { }
        public void SetFileTypes(uint count, nint filters) { }
        public void SetFileTypeIndex(uint index) { }
        public void GetFileTypeIndex(out uint index) => index = 0;
        public void Advise(nint events, out uint cookie) => cookie = 0;
        public void Unadvise(uint cookie) { }
        public void SetDefaultFolder(IShellItem item) { }
        public IShellItem GetFolder() => null!;
        public IShellItem GetCurrentSelection() => null!;
        public void SetFileName(string name) { }
        public string GetFileName() => "";
        public void SetFileNameLabel(string label) { }
        public void AddPlace(IShellItem item, uint position) { }
        public void SetDefaultExtension(string extension) { }
        public void Close(int status) { }
        public void ClearClientData() { }
        public void SetFilter(nint filter) { }
    }

    private sealed class FakeShellItem : IShellItem
    {
        public string Path { get; init; } = "";
        public uint NameType { get; private set; }
        public string GetDisplayName(uint nameType) { NameType = nameType; return Path; }
        public void BindToHandler(nint context, ref Guid handler, ref Guid iid, out nint result) => result = nint.Zero;
        public IShellItem GetParent() => null!;
        public void GetAttributes(uint mask, out uint attributes) => attributes = 0;
        public void Compare(IShellItem other, uint hint, out int order) => order = 0;
    }
}
