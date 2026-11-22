namespace GitCosmos.Application.Interfaces.FileSystem;

public interface IChildFolderReader
{
    IReadOnlyList<string> GetImmediateFolders(string parentPath, CancellationToken cancellationToken = default);
}
