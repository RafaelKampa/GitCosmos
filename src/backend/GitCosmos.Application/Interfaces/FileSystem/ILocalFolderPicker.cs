namespace GitCosmos.Application.Interfaces.FileSystem;

public interface ILocalFolderPicker
{
    Task<string?> PickAsync(string title, string confirmLabel, string? initialPath, CancellationToken cancellationToken = default);
}
