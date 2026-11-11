using DevTools.Domain.Git;
using DevTools.Domain.Settings;

namespace DevTools.Application.Git;

/// <summary>Works out where a new worktree goes, from the worktree folder setting and the path template.</summary>
public static class WorktreePathBuilder
{
    /// <returns>The folder for the worktree, or <see langword="null"/> when no worktree folder is configured.</returns>
    public static string? Build(DevToolsSettings settings, GitRepository repository, string branch)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(repository);

        var root = settings.WorktreeRoots.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r));
        if (root is null)
        {
            return null;
        }

        var template = string.IsNullOrWhiteSpace(settings.WorktreePathTemplate)
            ? DevToolsSettings.DefaultWorktreePathTemplate
            : settings.WorktreePathTemplate;

        var path = template
            .Replace(
                "{worktreeRoot}",
                Environment.ExpandEnvironmentVariables(root.Trim()),
                StringComparison.OrdinalIgnoreCase
            )
            .Replace("{repo}", repository.Name, StringComparison.OrdinalIgnoreCase)
            .Replace("{branch}", BranchNames.ToFolderName(branch), StringComparison.OrdinalIgnoreCase)
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        return Path.GetFullPath(path);
    }
}
