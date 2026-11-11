using DevTools.Domain.Projects;
using DevTools.Domain.Tools;
using DevTools.Infrastructure;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DevTools.CommandPalette.Pages;

/// <summary>
/// The results of every tool on one page, grouped per tool: <c>Shop.slnx (feature/cart)</c> under Visual Studio and
/// again under Rider. A top-level command of its own, so it can have its own hotkey next to the per-tool ones.
/// </summary>
internal sealed partial class AllToolsPage : LaunchTargetsPage
{
    public AllToolsPage(DevToolsServices services)
        : base(services)
    {
        Id = CommandIds.AllTools;
        Name = "Open";
        Title = "All dev tools";
        Icon = Icons.Extension;
        PlaceholderText = "Search projects, solutions, worktrees, branches and tools";
    }

    // A project here opens with any tool, so it keeps the repository icon; its entries show each tool's.
    protected override IconInfo ProjectIcon => Icons.Repository;

    protected override string ProjectTitle(Project project) => $"Open {project.Name}";

    protected override IEnumerable<ToolDefinition> Tools(IReadOnlyList<ToolDefinition> installed) => installed;

    protected override void Decorate(ListItem item, LaunchTarget target)
    {
        item.Subtitle = $"{target.Tool.DisplayName} · {target.Path}";
        item.Section = target.Tool.DisplayName;
    }
}
