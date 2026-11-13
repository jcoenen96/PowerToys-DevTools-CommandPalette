namespace DevTools.Domain.Git;

/// <summary>A remote from the repository's git config, such as <c>origin</c>.</summary>
public sealed record GitRemote(string Name, string Url)
{
    /// <summary>The remote's web page, e.g. <c>https://github.com/me/shop</c>; <see langword="null"/> for a local remote.</summary>
    public string? WebUrl => RemoteUrls.ToWebUrl(Url);
}
