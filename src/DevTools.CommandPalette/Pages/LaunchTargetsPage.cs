using DevTools.Application.Repositories;
using DevTools.CommandPalette.Commands;
using DevTools.Domain.Projects;
using DevTools.Domain.Tools;
using DevTools.Infrastructure;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DevTools.CommandPalette.Pages;

/// <summary>
/// A searchable list of launch targets. The page filters itself (it is a dynamic list) so that, with the
/// "show worktrees only when searching" setting on, the first list has just the clones and worktrees join the results
/// as soon as the user types.
/// </summary>
internal abstract partial class LaunchTargetsPage : DynamicListPage
{
    private readonly Lock _lock = new();
    private List<(LaunchTarget Target, ListItem Item)>? _items;
    private DateTimeOffset _builtAt;
    private bool _refresh;

    protected LaunchTargetsPage(DevToolsServices services)
    {
        Services = services;
        EmptyContent = EmptyStates.NoRepositories(services);
    }

    protected DevToolsServices Services { get; }

    /// <summary>The tools whose targets this page lists, in the order they are shown.</summary>
    protected abstract IEnumerable<ToolDefinition> Tools(IReadOnlyList<ToolDefinition> installed);

    /// <summary>Lets a page add the tool to each item, e.g. a section per tool.</summary>
    protected virtual void Decorate(ListItem item, LaunchTarget target) { }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        if (!string.Equals(oldSearch, newSearch, StringComparison.Ordinal))
        {
            RaiseItemsChanged();
        }
    }

    public override IListItem[] GetItems()
    {
        var all = Items();
        var search = SearchText;
        var settings = Services.Settings.Current;
        var visible = all.Where(entry => LaunchTargetFilter.IsVisible(entry.Target, search, settings))
            .Select(entry => (IListItem)entry.Item);

        return string.IsNullOrWhiteSpace(search) ? [.. visible] : [.. ListHelpers.FilterList(visible, search)];
    }

    private List<(LaunchTarget Target, ListItem Item)> Items()
    {
        lock (_lock)
        {
            var stale = DateTimeOffset.UtcNow - _builtAt >= RepositoryService.CacheDuration;
            if (_items is not null && !_refresh && !stale)
            {
                return _items;
            }
        }

        IsLoading = true;
        try
        {
            bool refresh;
            lock (_lock)
            {
                refresh = _refresh;
                _refresh = false;
            }

            var installed = Services.Tools.GetInstalledTools();
            var items = new List<(LaunchTarget, ListItem)>();
            foreach (var tool in Tools(installed))
            {
                foreach (var target in Services.Targets.GetTargets(tool, refresh))
                {
                    var item = new ListItem(new LaunchCommand(Services, target))
                    {
                        Title = target.Title,
                        Subtitle = target.Path,
                        Icon = Icons.For(target.Repository),
                        Tags = Tags.For(target.Repository),
                        TextToSuggest = target.Name,
                        MoreCommands = RepositoryMenu.For(Services, target.Repository, installed, Refresh, target),
                    };
                    Decorate(item, target);
                    items.Add((target, item));
                }

                // Only the first tool may rescan the folders; the others reuse that scan.
                refresh = false;
            }

            lock (_lock)
            {
                _items = items;
                _builtAt = DateTimeOffset.UtcNow;
            }

            return items;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void Refresh()
    {
        lock (_lock)
        {
            _refresh = true;
        }

        RaiseItemsChanged();
    }
}
