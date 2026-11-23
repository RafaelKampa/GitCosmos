using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.Interfaces.Git;

namespace GitCosmos.Infrastructure.Git;

public class RepositoryStatusReader(IGitProcessRunner runner, IGitRepositoryInspector inspector) : IRepositoryStatusReader
{
    private static readonly SemaphoreSlim Slots = new(2, 2);

    public async Task<GitWorkingTreeStatus> ReadAsync(string localPath, CancellationToken cancellationToken = default)
    {
        await Slots.WaitAsync(cancellationToken);
        try
        {
            var repository = await inspector.InspectAsync(localPath, cancellationToken);
            // A moved/deleted working tree must not silently report its ancestor's state.
            if (!string.Equals(repository.LocalPath, Path.TrimEndingDirectorySeparator(Path.GetFullPath(localPath)), StringComparison.OrdinalIgnoreCase))
                throw new ValidationAppException(LocalizedText.Get("dashboard.root_changed"));
            var result = await runner.RunAsync(repository.LocalPath,
                ["-c", "core.fsmonitor=false", "status", "--porcelain=v2", "--branch", "-z", "--untracked-files=all", "--ignore-submodules=none", "--renames", "--ahead-behind"], cancellationToken);
            if (result.ExitCode != 0) throw new OperationFailedAppException(LocalizedText.Get("dashboard.status_failed"));
            return GitStatusParser.Parse(result.Output);
        }
        finally { Slots.Release(); }
    }
}
