namespace DevTools.Domain.Repositories;

/// <summary>How repositories are sorted. Pinned repositories always come first.</summary>
public enum RepositoryOrder
{
    /// <summary>Alphabetically by name.</summary>
    Name,

    /// <summary>The repository used last (opened, or a git command run in it) first.</summary>
    RecentlyUsed,

    /// <summary>The repository used most often in the last 30 days first.</summary>
    MostUsed,
}
