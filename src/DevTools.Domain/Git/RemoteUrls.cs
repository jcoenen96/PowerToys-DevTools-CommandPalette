namespace DevTools.Domain.Git;

/// <summary>
/// Turns git remote URLs into the web pages of the hosts behind them (GitHub, GitLab, Azure DevOps, Bitbucket and
/// anything that serves the repository at its own path), and builds the links to a branch and a new pull request.
/// </summary>
public static class RemoteUrls
{
    /// <summary>
    /// The web page of a remote: <c>git@github.com:me/shop.git</c> and <c>https://github.com/me/shop.git</c> both
    /// become <c>https://github.com/me/shop</c>. Local paths and <c>file://</c> remotes have none.
    /// </summary>
    public static string? ToWebUrl(string? remoteUrl)
    {
        if (string.IsNullOrWhiteSpace(remoteUrl))
        {
            return null;
        }

        var url = remoteUrl.Trim();
        string host;
        string path;

        if (
            url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("git://", StringComparison.OrdinalIgnoreCase)
        )
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
            {
                return null;
            }

            host = uri.Host;
            path = uri.AbsolutePath;
            if (uri.Scheme is "http" or "https" && !uri.IsDefaultPort)
            {
                host = $"{uri.Host}:{uri.Port}";
            }
        }
        else if (ScpLike(url) is { } scp)
        {
            (host, path) = scp;
        }
        else
        {
            return null;
        }

        path = path.Trim('/');
        if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            path = path[..^4];
        }

        if (path.Length == 0)
        {
            return null;
        }

        // Azure DevOps over SSH: ssh.dev.azure.com:v3/org/project/repo and org@vs-ssh.visualstudio.com:v3/org/project/repo.
        var parts = path.Split('/');
        if (parts is ["v3", var org, var project, var repo])
        {
            if (host.Equals("ssh.dev.azure.com", StringComparison.OrdinalIgnoreCase))
            {
                return $"https://dev.azure.com/{org}/{project}/_git/{repo}";
            }

            if (host.EndsWith("vs-ssh.visualstudio.com", StringComparison.OrdinalIgnoreCase))
            {
                return $"https://{org}.visualstudio.com/{project}/_git/{repo}";
            }
        }

        return $"https://{host}/{path}";
    }

    /// <summary>The page of a branch on the remote's website.</summary>
    public static string? BranchUrl(string? webUrl, string? branch)
    {
        if (string.IsNullOrWhiteSpace(webUrl) || string.IsNullOrWhiteSpace(branch))
        {
            return null;
        }

        var escaped = EscapePath(branch);
        return HostOf(webUrl) switch
        {
            Host.AzureDevOps => $"{webUrl}?version=GB{Uri.EscapeDataString(branch)}",
            Host.GitLab => $"{webUrl}/-/tree/{escaped}",
            Host.Bitbucket => $"{webUrl}/src/{escaped}",
            _ => $"{webUrl}/tree/{escaped}",
        };
    }

    /// <summary>The page that starts a pull (or merge) request for a branch.</summary>
    public static string? PullRequestUrl(string? webUrl, string? branch)
    {
        if (string.IsNullOrWhiteSpace(webUrl) || string.IsNullOrWhiteSpace(branch))
        {
            return null;
        }

        var escaped = Uri.EscapeDataString(branch);
        return HostOf(webUrl) switch
        {
            Host.AzureDevOps => $"{webUrl}/pullrequestcreate?sourceRef={escaped}",
            Host.GitLab => $"{webUrl}/-/merge_requests/new?merge_request%5Bsource_branch%5D={escaped}",
            Host.Bitbucket => $"{webUrl}/pull-requests/new?source={escaped}",
            _ => $"{webUrl}/compare/{EscapePath(branch)}?expand=1",
        };
    }

    private enum Host
    {
        Other,
        AzureDevOps,
        GitLab,
        Bitbucket,
    }

    private static Host HostOf(string webUrl) =>
        webUrl.Contains("/_git/", StringComparison.OrdinalIgnoreCase) ? Host.AzureDevOps
        : webUrl.Contains("gitlab", StringComparison.OrdinalIgnoreCase) ? Host.GitLab
        : webUrl.Contains("bitbucket.org", StringComparison.OrdinalIgnoreCase) ? Host.Bitbucket
        : Host.Other;

    private static string EscapePath(string branch) => string.Join('/', branch.Split('/').Select(Uri.EscapeDataString));

    /// <summary><c>user@host:path</c>, the short form of an SSH remote. Windows paths like <c>C:\repo</c> are not.</summary>
    private static (string Host, string Path)? ScpLike(string url)
    {
        var colon = url.IndexOf(':', StringComparison.Ordinal);
        // Not a Windows path (C:\repo) nor another URL scheme (file:///srv/repo).
        if (colon <= 1 || url.Contains('\\', StringComparison.Ordinal) || url.AsSpan(colon + 1).StartsWith("//"))
        {
            return null;
        }

        var authority = url[..colon];
        var at = authority.LastIndexOf('@');
        var host = at >= 0 ? authority[(at + 1)..] : authority;
        if (host.Length == 0 || host.Contains('/', StringComparison.Ordinal))
        {
            return null;
        }

        return (host, url[(colon + 1)..]);
    }
}
