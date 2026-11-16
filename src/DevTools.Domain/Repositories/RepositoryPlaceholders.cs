namespace DevTools.Domain.Repositories;

/// <summary>The placeholders the text of a custom repository action may contain.</summary>
public static class RepositoryPlaceholders
{
    /// <summary>The root folder of the repository or worktree.</summary>
    public const string Path = "{path}";

    /// <summary>The repository name.</summary>
    public const string Name = "{name}";

    /// <summary>The checked-out branch, or the short commit for a detached HEAD.</summary>
    public const string Branch = "{branch}";

    /// <summary>The URL of the primary remote (<c>origin</c>).</summary>
    public const string RemoteUrl = "{remoteUrl}";

    /// <summary>The web page of the primary remote, e.g. <c>https://github.com/me/shop</c>.</summary>
    public const string WebUrl = "{webUrl}";

    /// <summary>With <see cref="RepositoryAction.ForEachFile"/>: the full path of the file.</summary>
    public const string File = "{file}";

    /// <summary>With <see cref="RepositoryAction.ForEachFile"/>: the file name without its extension.</summary>
    public const string FileName = "{fileName}";
}
