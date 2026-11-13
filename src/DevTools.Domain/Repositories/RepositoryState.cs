namespace DevTools.Domain.Repositories;

/// <summary>What the extension remembers about repositories: pins, ignored ones, usage and saved searches.</summary>
public sealed record RepositoryState
{
    public static RepositoryState Empty { get; } = new();

    /// <summary>Full paths of pinned repositories.</summary>
    public IReadOnlyList<string> Pinned { get; init; } = [];

    /// <summary>Full paths of repositories hidden from every list.</summary>
    public IReadOnlyList<string> Ignored { get; init; } = [];

    /// <summary>Per repository path, when it was used (newest last, the last 30 days).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<DateTimeOffset>> Usage { get; init; } =
        new Dictionary<string, IReadOnlyList<DateTimeOffset>>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<SavedSearch> SavedSearches { get; init; } = [];
}
