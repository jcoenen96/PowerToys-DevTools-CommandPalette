using DevTools.Application.Git;
using DevTools.CommandPalette.Commands;
using DevTools.Domain.Git;
using DevTools.Domain.Projects;
using DevTools.Domain.Tools;
using DevTools.Infrastructure;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DevTools.CommandPalette.Pages;

/// <summary>
/// The "more commands" (Ctrl+K) menu shared by every repository and launch target in the palette. In the git pages
/// (no <c>current</c> target) the git commands come first; on a tool's page, opening it in another tool does.
/// </summary>
internal static class RepositoryMenu
{
    public static IContextItem[] For(
        DevToolsServices services,
        GitRepository repository,
        IReadOnlyList<ToolDefinition> installedTools,
        Action refresh,
        LaunchTarget? current = null
    )
    {
        var items = new List<IContextItem>();
        var gitFirst = current is null;
        if (gitFirst)
        {
            items.AddRange(GitItems(services, repository));
        }

        foreach (var tool in installedTools)
        {
            if (current is not null && string.Equals(tool.Id, current.Tool.Id, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (TargetFor(services, tool, repository, current) is { } target)
            {
                items.Add(new CommandContextItem(new LaunchCommand(services, target)));
            }
        }

        if (!gitFirst)
        {
            items.Add(new CommandContextItem(new RepositoryPage(services, repository)) { Title = "Git commands" });
        }

        // Custom actions, the remote on the web, then pin and ignore.
        items.AddRange(RepositoryExtras.ActionItems(services, repository).Select(RepositoryExtras.ToContextItem));
        items.AddRange(RepositoryExtras.RemoteItems(repository).Select(RepositoryExtras.ToContextItem));
        var manage = RepositoryExtras.ManageItems(services, repository, refresh).ToArray();
        items.Add(RepositoryExtras.ToContextItem(manage[0]));

        items.Add(
            new CommandContextItem(new CopyTextCommand(current?.Path ?? repository.Path) { Name = "Copy path" })
            {
                Icon = Icons.Copy,
            }
        );
        items.Add(
            new CommandContextItem(new CopyTextCommand(repository.Head.DisplayName) { Name = "Copy branch name" })
            {
                Icon = Icons.Branch,
            }
        );
        items.Add(new CommandContextItem(new RefreshCommand(refresh)));
        items.AddRange(manage.Skip(1).Select(RepositoryExtras.ToContextItem));
        return [.. items];
    }

    /// <summary>The repository's git commands: branches, worktrees, then fetch, pull, push and the rest.</summary>
    private static IEnumerable<IContextItem> GitItems(DevToolsServices services, GitRepository repository)
    {
        yield return new CommandContextItem(new BranchesPage(services, repository, BranchAction.Checkout))
        {
            Title = "Switch branch",
            Icon = Icons.Branch,
        };
        yield return new CommandContextItem(new NewBranchPage(services, repository, inNewWorktree: false))
        {
            Title = "New branch",
            Icon = Icons.NewBranch,
        };
        yield return new CommandContextItem(new BranchesPage(services, repository, BranchAction.AddWorktree))
        {
            Title = "New worktree for a branch",
            Icon = Icons.Worktree,
        };
        yield return new CommandContextItem(new NewBranchPage(services, repository, inNewWorktree: true))
        {
            Title = "New branch in a new worktree",
            Icon = Icons.Worktree,
        };
        yield return new CommandContextItem(new WorktreesPage(services, repository))
        {
            Title = "Worktrees",
            Icon = Icons.Worktree,
        };

        foreach (var command in GitCommands.All)
        {
            yield return new CommandContextItem(GitCommandFactory.For(services, repository, command))
            {
                Title = command.Title,
                Subtitle = command.Description,
                Icon = new IconInfo(command.Icon),
                IsCritical = command.RequiresConfirmation,
            };
        }
    }

    /// <summary>
    /// What another tool should open for the same item: the same file when the tool opens such files (Shop.slnx in
    /// Rider rather than Visual Studio), otherwise the tool's first target in the repository.
    /// </summary>
    private static LaunchTarget? TargetFor(
        DevToolsServices services,
        ToolDefinition tool,
        GitRepository repository,
        LaunchTarget? current
    )
    {
        if (
            current is { IsFolder: false }
            && tool.TargetKind == ToolTargetKind.Files
            && tool.FilePatterns.Any(pattern =>
                current.Path.EndsWith(pattern.TrimStart('*'), StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            return current with { Tool = tool };
        }

        if (tool.TargetKind == ToolTargetKind.Folder)
        {
            return new LaunchTarget(tool, repository, repository.Path, IsFolder: true);
        }

        var targets = services.Targets.GetTargets(tool, repository);
        return targets.Count > 0 ? targets[0] : null;
    }
}
