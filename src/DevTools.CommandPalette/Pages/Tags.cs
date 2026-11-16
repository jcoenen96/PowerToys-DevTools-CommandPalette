using DevTools.Application.Git;
using DevTools.Application.Repositories;
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

    private static readonly Tag PinnedTag = new("pinned") { Icon = Icons.Pin, ToolTip = "Pinned: listed first" };

    public static ITag[] For(GitRepository repository) =>
        repository.IsWorktree ? [WorktreeTag]
        : repository.Head.IsDetached ? [new Tag("detached")]
        : [];

    /// <summary>Pinned, worktree or detached, the tags from repositories.json, then the git status when known.</summary>
    public static ITag[] For(RepositoryEntry entry, GitStatus? status = null)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var tags = new List<ITag>(For(entry.IsPinned, []));
        tags.AddRange(For(entry.Repository));
        tags.AddRange(UserTags(entry.Tags));
        if (status is not null)
        {
            tags.AddRange(StatusTags(status));
        }

        return [.. tags];
    }

    /// <summary>The pinned tag and the tags from repositories.json, e.g. for a project with all its checkouts.</summary>
    public static ITag[] For(bool isPinned, IEnumerable<string> tags) =>
        [.. isPinned ? [PinnedTag] : Array.Empty<ITag>(), .. UserTags(tags.Distinct(StringComparer.OrdinalIgnoreCase))];

    private static IEnumerable<ITag> UserTags(IEnumerable<string> tags) =>
        tags.Select(tag => new Tag(tag) { Icon = Icons.Tag, ToolTip = $"Search with tag:{tag}" });

    /// <summary><c>↑2</c>, <c>↓1</c>, <c>3 changed</c> and <c>1 untracked</c>, or nothing for a clean, current repository.</summary>
    public static IEnumerable<ITag> StatusTags(GitStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        if (status.Ahead > 0)
        {
            yield return new Tag($"↑{status.Ahead}") { ToolTip = $"{status.Ahead} commits to push" };
        }

        if (status.Behind > 0)
        {
            yield return new Tag($"↓{status.Behind}") { ToolTip = $"{status.Behind} commits to pull" };
        }

        if (status.Changed > 0)
        {
            yield return new Tag($"{status.Changed} changed") { ToolTip = "Changed files" };
        }

        if (status.Untracked > 0)
        {
            yield return new Tag($"{status.Untracked} untracked") { ToolTip = "New files git doesn't track yet" };
        }
    }
}
