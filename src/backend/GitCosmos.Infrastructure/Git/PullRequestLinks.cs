using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;

namespace GitCosmos.Infrastructure.Git;

public static class PullRequestLinks
{
    public static string Create(string remote, string source, string target)
    {
        if (remote.StartsWith("git@", StringComparison.Ordinal) && remote.Contains(':')) remote = "https://" + remote[4..].Replace(':', '/');
        if (!Uri.TryCreate(remote, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "ssh") || uri.Port is not (-1 or 22 or 443) || uri.Query.Length > 0 || uri.Fragment.Length > 0) throw Unsupported();
        var path = uri.AbsolutePath.TrimEnd('/'); if (path.EndsWith(".git", StringComparison.Ordinal)) path = path[..^4];
        var host = uri.Host.ToLowerInvariant(); var root = "https://" + host + path;
        if (host is "ssh.dev.azure.com" or "vs-ssh.visualstudio.com")
        {
            var pieces = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (pieces.Length != 4 || pieces[0] != "v3") throw Unsupported();
            root = "https://dev.azure.com/" + pieces[1] + "/" + pieces[2] + "/_git/" + pieces[3]; host = "dev.azure.com";
        }
        var from = Uri.EscapeDataString(source); var to = Uri.EscapeDataString(target);
        if (host == "github.com") return root + "/compare/" + to + "..." + from + "?expand=1";
        if (host == "gitlab.com") return root + "/-/merge_requests/new?merge_request%5Bsource_branch%5D=" + from + "&merge_request%5Btarget_branch%5D=" + to;
        if (host == "bitbucket.org") return root + "/pull-requests/new?source=" + from + "&dest=" + to;
        if (host == "dev.azure.com" || host.EndsWith(".visualstudio.com", StringComparison.Ordinal)) return root + "/pullrequestcreate?sourceRef=" + Uri.EscapeDataString("refs/heads/" + source) + "&targetRef=" + Uri.EscapeDataString("refs/heads/" + target);
        throw Unsupported();
    }
    private static ValidationAppException Unsupported() => new(LocalizedText.Get("branch.pr_provider"));
}
