namespace DevTools.Application.Git;

/// <summary>A git command that runs as is in a repository, without further input.</summary>
public sealed record GitCommand(string Id, string Title, string Description, IReadOnlyList<string> Arguments)
{
    /// <summary>A Segoe Fluent icon glyph.</summary>
    public string Icon { get; init; } = "\uE895";

    /// <summary>Asks before running, for commands that change or throw away local work.</summary>
    public bool RequiresConfirmation { get; init; }

    /// <summary>Shows git's output in the palette instead of a short toast.</summary>
    public bool ShowsOutput { get; init; }
}

public static class GitCommands
{
    public static GitCommand Fetch { get; } =
        new("fetch", "Fetch", "git fetch --all --prune", ["fetch", "--all", "--prune"]) { Icon = "\uE896" };

    public static GitCommand Pull { get; } =
        new("pull", "Pull", "git pull --ff-only", ["pull", "--ff-only"]) { Icon = "\uE74B" };

    public static GitCommand PullRebase { get; } =
        new("pull-rebase", "Pull (rebase)", "git pull --rebase --autostash", ["pull", "--rebase", "--autostash"])
        {
            Icon = "\uE74B",
        };

    public static GitCommand Push { get; } =
        new("push", "Push", "git push (sets the upstream for a new branch)", ["push", "-u", "origin", "HEAD"])
        {
            Icon = "\uE74A",
        };

    public static GitCommand Status { get; } =
        new("status", "Status", "git status", ["status"]) { Icon = "\uE9D9", ShowsOutput = true };

    public static GitCommand Log { get; } =
        new("log", "Recent commits", "git log --oneline -20", ["log", "--oneline", "--decorate", "-20"])
        {
            Icon = "\uE81C",
            ShowsOutput = true,
        };

    public static GitCommand Stash { get; } =
        new("stash", "Stash changes", "git stash push --include-untracked", ["stash", "push", "--include-untracked"])
        {
            Icon = "\uE7B8",
        };

    public static GitCommand StashPop { get; } =
        new("stash-pop", "Pop stash", "git stash pop", ["stash", "pop"]) { Icon = "\uE7B8" };

    public static GitCommand PruneWorktrees { get; } =
        new("worktree-prune", "Prune worktrees", "git worktree prune", ["worktree", "prune"]) { Icon = "\uE74D" };

    public static GitCommand DiscardChanges { get; } =
        new("discard", "Discard all changes", "git reset --hard (untracked files stay)", ["reset", "--hard"])
        {
            Icon = "\uE7A7",
            RequiresConfirmation = true,
        };

    public static IReadOnlyList<GitCommand> All { get; } =
    [Fetch, Pull, PullRebase, Push, Status, Log, Stash, StashPop, PruneWorktrees, DiscardChanges];
}
