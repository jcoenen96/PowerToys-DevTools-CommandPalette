namespace DevTools.Domain.Git;

/// <summary>A clone or worktree found under one of the configured folders.</summary>
public sealed record GitRepository(string Name, string Path, GitHead Head, RepositoryKind Kind)
{
    /// <summary>For a worktree: the working folder of the repository it belongs to, when known.</summary>
    public string? MainRepositoryPath { get; init; }

    public bool IsWorktree => Kind == RepositoryKind.Worktree;
}
