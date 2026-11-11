using DevTools.Domain.Git;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DevTools.CommandPalette.Pages;

internal static class Tags
{
    private static readonly Tag WorktreeTag = new("worktree")
    {
        Icon = Icons.Worktree,
        ToolTip = "A linked git worktree",
    };

    public static ITag[] For(GitRepository repository) =>
        repository.IsWorktree ? [WorktreeTag]
        : repository.Head.IsDetached ? [new Tag("detached")]
        : [];
}
