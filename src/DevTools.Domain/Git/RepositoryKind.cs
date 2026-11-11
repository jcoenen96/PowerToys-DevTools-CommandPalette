namespace DevTools.Domain.Git;

public enum RepositoryKind
{
    /// <summary>A regular clone with its own <c>.git</c> folder.</summary>
    Clone,

    /// <summary>A linked worktree (<c>git worktree add</c>); its <c>.git</c> is a file pointing to the main repository.</summary>
    Worktree,
}
