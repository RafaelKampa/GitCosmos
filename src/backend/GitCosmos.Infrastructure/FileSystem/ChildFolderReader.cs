using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.Interfaces.FileSystem;

namespace GitCosmos.Infrastructure.FileSystem;

public class ChildFolderReader : IChildFolderReader
{
    public IReadOnlyList<string> GetImmediateFolders(string parentPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var parent = parentPath.Trim();
            if (!Path.IsPathFullyQualified(parent) || !Directory.Exists(parent))
                throw new ValidationAppException(LocalizedText.Get("repository.invalid_path"));
            // No recursion or directory links: the selected folder is only a container.
            var options = new EnumerationOptions { RecurseSubdirectories = false, IgnoreInaccessible = false,
                AttributesToSkip = FileAttributes.ReparsePoint, ReturnSpecialDirectories = false };
            var result = new List<string>();
            foreach (var child in Directory.EnumerateDirectories(Path.GetFullPath(parent), "*", options))
            {
                cancellationToken.ThrowIfCancellationRequested();
                result.Add(child);
            }
            return result.Order(StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new ValidationAppException(LocalizedText.Get("import.unreadable"));
        }
    }
}
