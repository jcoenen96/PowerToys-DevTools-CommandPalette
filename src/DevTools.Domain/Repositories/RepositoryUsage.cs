namespace DevTools.Domain.Repositories;

/// <summary>How often and when a repository was used from the palette.</summary>
public sealed record RepositoryUsage(int Count, DateTimeOffset? LastUsed)
{
    public static RepositoryUsage None { get; } = new(0, null);
}
