using DevTools.CommandPalette.Commands;
using DevTools.Infrastructure;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DevTools.CommandPalette.Pages;

/// <summary>
/// The results of every tool on one page, grouped per tool: <c>Shop.slnx (feature/cart)</c> under Visual Studio and
/// again under Rider. A top-level command of its own, so it can have its own hotkey next to the per-tool ones.
/// </summary>
internal sealed partial class AllToolsPage : ListPage
{
    private readonly DevToolsServices _services;
    private bool _refresh;

    public AllToolsPage(DevToolsServices services)
    {
        _services = services;
        Id = CommandIds.AllTools;
        Name = "Open";
        Title = "All dev tools";
        Icon = Icons.Extension;
        PlaceholderText = "Search solutions, repositories, branches and tools";
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
            var items = new List<IListItem>();
            foreach (var tool in installed.Where(tool => tool.ShowAsTopLevelCommand))
            {
                var icon = Icons.For(tool, _services.Resolver);
                items.AddRange(
                    _services
                        .Targets.GetTargets(tool, refresh)
                        .Select(target => new ListItem(new LaunchCommand(_services, target))
                        {
                            Title = target.Title,
                            Subtitle = $"{tool.DisplayName} · {target.Path}",
                            Icon = icon,
                            Tags = Tags.For(target.Repository),
                            Section = tool.DisplayName,
                            TextToSuggest = target.Name,
                            MoreCommands = RepositoryMenu.For(_services, target.Repository, installed, Refresh, target),
                        })
                );

                // Only the first tool may rescan; the others reuse that scan.
                refresh = false;
            }

            return [.. items];
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
