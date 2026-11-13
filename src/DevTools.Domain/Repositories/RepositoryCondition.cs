namespace DevTools.Domain.Repositories;

/// <summary>
/// When a tag or an action applies to a repository. Every field that is set must match; an empty condition matches
/// every repository. Text matches ignore case.
/// </summary>
public sealed record RepositoryCondition
{
    public static RepositoryCondition Always { get; } = new();

    /// <summary>The repository name, with <c>*</c> and <c>?</c> wildcards (<c>Shop*</c>).</summary>
    public string? Name { get; init; }

    /// <summary>Part of the repository path, with either slash (<c>source/repos/work</c>).</summary>
    public string? Path { get; init; }

    /// <summary>Part of the URL of any remote (<c>github.com</c>, <c>dev.azure.com/my-org</c>).</summary>
    public string? Remote { get; init; }

    /// <summary>The checked-out branch, with wildcards (<c>feature/*</c>).</summary>
    public string? Branch { get; init; }

    /// <summary>A file or folder in the repository, relative to its root; wildcards in the last part (<c>*.slnx</c>).</summary>
    public string? FileExists { get; init; }

    /// <summary>A tag the repository has (for actions; tags themselves can't depend on tags).</summary>
    public string? Tag { get; init; }

    /// <summary><see langword="true"/> for worktrees only, <see langword="false"/> for clones only.</summary>
    public bool? IsWorktree { get; init; }

    public bool IsEmpty => this == Always;
}
