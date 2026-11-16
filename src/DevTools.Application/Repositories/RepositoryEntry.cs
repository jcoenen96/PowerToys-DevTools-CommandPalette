using DevTools.Domain.Git;
using DevTools.Domain.Repositories;

namespace DevTools.Application.Repositories;

/// <summary>A repository with what the user added to it: its tags, whether it is pinned, and its usage.</summary>
public sealed record RepositoryEntry(
    GitRepository Repository,
    IReadOnlyList<string> Tags,
    bool IsPinned,
    RepositoryUsage Usage
);
