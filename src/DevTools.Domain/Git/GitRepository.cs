namespace DevTools.Domain.Git;

/// <summary>A clone or worktree found under one of the configured folders.</summary>
public sealed record GitRepository(string Name, string Path, GitHead Head, RepositoryKind Kind)
{
    /// <summary>For a worktree: the working folder of the repository it belongs to, when known.</summary>
    public string? MainRepositoryPath { get; init; }

    /// <summary>The remotes from the repository's git config, <c>origin</c> first.</summary>
    public IReadOnlyList<GitRemote> Remotes { get; init; } = [];

    /// <summary>The remote pull requests and web pages are for: <c>origin</c>, or else the first one.</summary>
    public GitRemote? PrimaryRemote => Remotes.Count > 0 ? Remotes[0] : null;

    public bool IsWorktree => Kind == RepositoryKind.Worktree;
}
