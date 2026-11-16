using DevTools.Infrastructure;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DevTools.CommandPalette.Pages;

internal static class EmptyStates
{
    public static ICommandItem NoRepositories(DevToolsServices services)
    {
        var settings = services.Settings.Current;
        var folders = string.Join(", ", settings.RepositoryRoots.Concat(settings.WorktreeRoots));
        return new CommandItem(new NoOpCommand())
        {
            Title = "No repositories found",
            Subtitle =
                folders.Length == 0
                    ? "Set your repository and worktree folders in the Dev tools settings."
                    : $"Nothing with a .git folder in {folders}. Check the folders in the Dev tools settings.",
            Icon = Icons.Info,
        };
    }
}
