using DevTools.CommandPalette.Commands;
using DevTools.Domain.Tools;
using DevTools.Infrastructure;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DevTools.CommandPalette.Pages;

/// <summary>
/// Everything one tool can open, across all repositories and worktrees: <c>Shop.slnx (feature/cart)</c>. Each tool
/// gets its own page as a top-level command, so it can have its own hotkey and alias in Command Palette.
/// </summary>
internal sealed partial class ToolTargetsPage : ListPage
{
    private readonly DevToolsServices _services;
    private readonly ToolDefinition _tool;
    private bool _refresh;

    public ToolTargetsPage(DevToolsServices services, ToolDefinition tool)
    {
        _services = services;
        _tool = tool;
        Id = CommandIds.Tool(tool.Id);
        Name = "Open";
        Title = $"Open in {tool.DisplayName}";
        Icon = Icons.For(tool, services.Resolver);
        PlaceholderText =
            tool.TargetKind == ToolTargetKind.Files
                ? "Search solutions, repositories and branches"
                : "Search repositories and branches";
        EmptyContent = EmptyStates.NoRepositories(services);
    }

    public override IListItem[] GetItems()
    {
        IsLoading = true;
        try
        {
            var refresh = _refresh;
            _refresh = false;

            var installed = _services.Tools.GetInstalledTools();
            return
            [
                .. _services
                    .Targets.GetTargets(_tool, refresh)
                    .Select(target => new ListItem(new LaunchCommand(_services, target))
                    {
                        Title = target.Title,
                        Subtitle = target.Path,
                        Icon = Icons.For(target.Repository),
                        Tags = Tags.For(target.Repository),
                        TextToSuggest = target.Name,
                        MoreCommands = RepositoryMenu.For(_services, target.Repository, installed, Refresh, target),
                    }),
            ];
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void Refresh()
    {
        _refresh = true;
        RaiseItemsChanged();
    }
}
