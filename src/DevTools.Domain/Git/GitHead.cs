namespace DevTools.Domain.Git;

/// <summary>What a repository or worktree has checked out: a branch, or a commit when HEAD is detached.</summary>
public sealed record GitHead(string? Branch, string? Commit)
{
    public static GitHead Unknown { get; } = new(null, null);

    public bool IsDetached => Branch is null && Commit is not null;

    /// <summary>The branch name, <c>detached@1a2b3c4</c>, or <c>unknown</c>.</summary>
    public string DisplayName =>
        Branch ?? (Commit is { Length: > 0 } commit ? $"detached@{commit[..Math.Min(7, commit.Length)]}" : "unknown");
}
