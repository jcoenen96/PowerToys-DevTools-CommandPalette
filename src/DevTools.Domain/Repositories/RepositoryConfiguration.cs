namespace DevTools.Domain.Repositories;

/// <summary>The user's <c>repositories.json</c>: tags, custom actions and saved searches.</summary>
public sealed record RepositoryConfiguration
{
    public static RepositoryConfiguration Empty { get; } = new();

    public IReadOnlyList<TagRule> Tags { get; init; } = [];

    public IReadOnlyList<RepositoryAction> Actions { get; init; } = [];

    public IReadOnlyList<SavedSearch> Searches { get; init; } = [];
}
