namespace DevTools.Domain.Git;

/// <summary>An entry of <c>git worktree list</c>.</summary>
public sealed record GitWorktree(string Path, GitHead Head, bool IsMain)
{
    public bool IsPrunable { get; init; }

    public bool IsLocked { get; init; }
}
