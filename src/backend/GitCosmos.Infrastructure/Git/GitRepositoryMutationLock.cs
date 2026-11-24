namespace GitCosmos.Infrastructure.Git;

internal static class GitRepositoryMutationLock
{
    private static readonly SemaphoreSlim[] Gates = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    public static SemaphoreSlim For(string commonDirectory) => Gates[(StringComparer.OrdinalIgnoreCase.GetHashCode(commonDirectory) & int.MaxValue) % Gates.Length];
}
