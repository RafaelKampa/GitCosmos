using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.Interfaces.Git;

namespace GitCosmos.Infrastructure.Git;

public class GitRepositoryInspector(IGitProcessRunner runner) : IGitRepositoryInspector
{
    public async Task<LocalGitRepository> InspectAsync(string localPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = localPath.Trim();
        if (!Path.IsPathFullyQualified(path) || !Directory.Exists(path))
            throw new ValidationAppException(LocalizedText.Get("repository.invalid_path"));

        var result = await runner.RunAsync(path, ["rev-parse", "--path-format=absolute", "--show-toplevel"], cancellationToken);
        if (result.ExitCode != 0)
            throw new ValidationAppException(LocalizedText.Get("repository.invalid_git"));

        string root;
        try
        {
            root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(result.Output.TrimEnd('\r', '\n')));
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            throw new ValidationAppException(LocalizedText.Get("repository.invalid_git"));
        }
        if (!Path.IsPathFullyQualified(result.Output.TrimEnd('\r', '\n')) || !Directory.Exists(root))
            throw new ValidationAppException(LocalizedText.Get("repository.invalid_git"));
        var name = new DirectoryInfo(root).Name;
        return new(name, root, root.ToUpperInvariant());
    }
}
