namespace DevTools.Domain.Git;

/// <summary>A local or remote-tracking branch.</summary>
public sealed record GitBranch(string Name, bool IsRemote, bool IsCurrent)
{
    /// <summary>The folder of the worktree that has this branch checked out, if any.</summary>
    public string? WorktreePath { get; init; }

    /// <summary>For a remote-tracking branch, the name without the remote (<c>origin/main</c> -> <c>main</c>).</summary>
    public string LocalName =>
        IsRemote && Name.IndexOf('/', StringComparison.Ordinal) is var i and > 0 ? Name[(i + 1)..] : Name;
}
