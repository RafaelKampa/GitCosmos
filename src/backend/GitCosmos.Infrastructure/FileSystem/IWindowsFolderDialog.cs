namespace GitCosmos.Infrastructure.FileSystem;

public interface IWindowsFolderDialog : IDisposable
{
    string? Show(string title, string confirmLabel, string? initialPath);
    void Close();
}

public interface IWindowsFolderDialogFactory
{
    bool IsSupported { get; }
    IWindowsFolderDialog Create();
}
