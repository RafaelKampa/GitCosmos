using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Infrastructure.Git;
namespace GitCosmos.Application.Tests;
public class GitBlobReaderTests
{
    [Fact]
    public async Task ReadsExactBinaryAndEmptyObjectsAndRejectsInvalidMissingAndOversizedObjects()
    {
        await using var fixture = await GitTestDirectory.CreateAsync(); var reader = new GitBlobReader("git", TimeSpan.FromSeconds(10));
        var path = Path.Combine(fixture.RepositoryPath, "binary.bin"); var bytes = new byte[] { 0, 255, 254, 128, 10, 13 };
        await File.WriteAllBytesAsync(path, bytes); var oid = (await fixture.RunAsync(fixture.RepositoryPath, "hash-object", "-w", "--", path)).Trim();
        Assert.Equal(bytes, await reader.ReadAsync(fixture.RepositoryPath, oid, default));
        await File.WriteAllBytesAsync(path, []); oid = (await fixture.RunAsync(fixture.RepositoryPath, "hash-object", "-w", "--", path)).Trim();
        Assert.Empty(await reader.ReadAsync(fixture.RepositoryPath, oid, default));
        await Assert.ThrowsAsync<ValidationAppException>(() => reader.ReadAsync(fixture.RepositoryPath, "HEAD", default));
        await Assert.ThrowsAsync<OperationFailedAppException>(() => reader.ReadAsync(fixture.RepositoryPath, new string('f', 40), default));
        await File.WriteAllBytesAsync(path, new byte[GitBlobReader.MaximumBytes + 1]); oid = (await fixture.RunAsync(fixture.RepositoryPath, "hash-object", "-w", "--", path)).Trim();
        await Assert.ThrowsAsync<ValidationAppException>(() => reader.ReadAsync(fixture.RepositoryPath, oid, default));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadAsync(fixture.RepositoryPath, oid, new CancellationToken(true)));
        await Assert.ThrowsAsync<OperationFailedAppException>(() => new GitBlobReader(Path.Combine(fixture.Root, "missing-git.exe"), TimeSpan.FromSeconds(10)).ReadAsync(fixture.RepositoryPath, oid, default));
    }
}
