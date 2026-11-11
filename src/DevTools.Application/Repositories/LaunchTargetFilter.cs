using DevTools.Domain.Projects;
using DevTools.Domain.Settings;

namespace DevTools.Application.Repositories;

/// <summary>Which launch targets a tool page shows before and after the user starts typing.</summary>
public static class LaunchTargetFilter
{
    /// <summary>
    /// Whether <paramref name="target"/> is on the list for <paramref name="searchText"/>: worktrees wait for a search
    /// when <see cref="DevToolsSettings.ShowWorktreesOnlyWhenSearching"/> is on; everything else always shows.
    /// </summary>
    public static bool IsVisible(LaunchTarget target, string? searchText, DevToolsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        return !target.Repository.IsWorktree
            || !settings.ShowWorktreesOnlyWhenSearching
            || !string.IsNullOrWhiteSpace(searchText);
    }
}
