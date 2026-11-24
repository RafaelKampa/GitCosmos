using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Infrastructure.Git;

namespace GitCosmos.Application.Tests;

public class PullRequestLinksTests
{
    [Theory]
    [InlineData("git@github.com:owner/repo.git", "https://github.com/owner/repo/compare/main...feature%2Fnew?expand=1")]
    [InlineData("ssh://git@github.com/owner/repo.git", "https://github.com/owner/repo/compare/main...feature%2Fnew?expand=1")]
    [InlineData("https://token@gitlab.com/group/repo.git", "https://gitlab.com/group/repo/-/merge_requests/new?merge_request%5Bsource_branch%5D=feature%2Fnew&merge_request%5Btarget_branch%5D=main")]
    [InlineData("https://bitbucket.org/owner/repo.git", "https://bitbucket.org/owner/repo/pull-requests/new?source=feature%2Fnew&dest=main")]
    [InlineData("https://dev.azure.com/org/project/_git/repo", "https://dev.azure.com/org/project/_git/repo/pullrequestcreate?sourceRef=refs%2Fheads%2Ffeature%2Fnew&targetRef=refs%2Fheads%2Fmain")]
    [InlineData("git@ssh.dev.azure.com:v3/org/project/repo", "https://dev.azure.com/org/project/_git/repo/pullrequestcreate?sourceRef=refs%2Fheads%2Ffeature%2Fnew&targetRef=refs%2Fheads%2Fmain")]
    [InlineData("https://org.visualstudio.com/project/_git/repo", "https://org.visualstudio.com/project/_git/repo/pullrequestcreate?sourceRef=refs%2Fheads%2Ffeature%2Fnew&targetRef=refs%2Fheads%2Fmain")]
    public void ProviderFormsEncodeBranchesAndNeverExposeCredentials(string remote, string expected) => Assert.Equal(expected, PullRequestLinks.Create(remote, "feature/new", "main"));
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://evil.example/repo")]
    [InlineData("http://github.com/owner/repo")]
    [InlineData("https://github.com:8443/owner/repo")]
    [InlineData("https://github.com/owner/repo?token=secret")]
    [InlineData("https://github.com/owner/repo#fragment")]
    [InlineData("git@ssh.dev.azure.com:bad/path")]
    public void UnsupportedProvidersAndUnsafeUrlsAreRejected(string url) => Assert.Throws<ValidationAppException>(() => PullRequestLinks.Create(url, "feature", "main"));
}
