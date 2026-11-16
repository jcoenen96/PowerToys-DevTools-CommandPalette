using DevTools.Domain.Projects;
using DevTools.Domain.Tools;
using DevTools.Infrastructure;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DevTools.CommandPalette.Pages;

/// <summary>
/// Everything one tool can open, across all repositories and worktrees: <c>Shop.slnx (feature/cart)</c>. Each tool
/// gets its own page as a top-level command, so it can have its own hotkey and alias in Command Palette.
/// </summary>
internal sealed partial class ToolTargetsPage : LaunchTargetsPage
{
    private readonly ToolDefinition _tool;

    public ToolTargetsPage(DevToolsServices services, ToolDefinition tool)
        : base(services)
    {
        _tool = tool;
        Id = CommandIds.Tool(tool.Id);
        Name = "Open";
        Title = $"Open in {tool.DisplayName}";
        Icon = Icons.For(tool, services.Resolver);
        PlaceholderText =
            tool.TargetKind == ToolTargetKind.Files
                ? "Search projects, solutions, worktrees and branches"
                : "Search projects, worktrees and branches";
    }

    protected override IconInfo ProjectIcon => Icon;

    protected override string ProjectTitle(Project project) => $"{project.Name} in {_tool.DisplayName}";

    protected override IEnumerable<ToolDefinition> Tools(IReadOnlyList<ToolDefinition> installed) =>
        (ToolDefinition[])[_tool];
}
