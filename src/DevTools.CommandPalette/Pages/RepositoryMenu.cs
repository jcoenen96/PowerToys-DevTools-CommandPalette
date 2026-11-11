using DevTools.CommandPalette.Commands;
using DevTools.Domain.Git;
using DevTools.Domain.Projects;
using DevTools.Domain.Tools;
using DevTools.Infrastructure;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DevTools.CommandPalette.Pages;

/// <summary>The "more commands" (Ctrl+K) menu shared by every repository and launch target in the palette.</summary>
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

        items.Add(new CommandContextItem(new RepositoryPage(services, repository)) { Title = "Git commands" });
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
        return [.. items];
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
