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
        PlaceholderText = "Search solutions, repositories, worktrees, branches and tools";
    }

    protected override IEnumerable<ToolDefinition> Tools(IReadOnlyList<ToolDefinition> installed) => installed;

    protected override void Decorate(ListItem item, LaunchTarget target)
    {
        item.Icon = Icons.For(target.Tool, Services.Resolver);
        item.Subtitle = $"{target.Tool.DisplayName} · {target.Path}";
        item.Section = target.Tool.DisplayName;
    }
}
