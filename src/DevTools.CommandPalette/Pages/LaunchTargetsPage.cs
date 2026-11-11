using DevTools.Application.Repositories;
using DevTools.CommandPalette.Commands;
using DevTools.Domain.Projects;
using DevTools.Domain.Tools;
using DevTools.Infrastructure;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DevTools.CommandPalette.Pages;

/// <summary>
/// What one or more tools can open. With "List projects first" on, the page lists the project names (<c>Shop</c>);
/// choosing one lists its clone first and then its worktrees, each with its branch: <c>Shop.slnx (main)</c>,
/// <c>Shop.slnx (feature/cart)</c>. Typing searches the projects and those entries together. The page filters itself
/// (it is a dynamic list).
/// </summary>
internal abstract partial class LaunchTargetsPage : DynamicListPage
{
    private readonly Lock _lock = new();
    private Snapshot? _snapshot;
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

    /// <summary>The title of a project's page, e.g. "Shop in Visual Studio".</summary>
    protected abstract string ProjectTitle(Project project);

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        if (!string.Equals(oldSearch, newSearch, StringComparison.Ordinal))
        {
            RaiseItemsChanged();
        }
    }

    public override IListItem[] GetItems()
    {
        var snapshot = Build();
        var search = SearchText;
        var searching = !string.IsNullOrWhiteSpace(search);

        if (!Services.Settings.Current.GroupByProject)
        {
            IEnumerable<IListItem> all = snapshot.Targets;
            return searching ? [.. ListHelpers.FilterList(all, search)] : [.. all];
        }

        IEnumerable<IListItem> projects = snapshot.Projects;
        if (!searching)
        {
            return [.. projects];
        }

        IEnumerable<IListItem> targets = snapshot.Targets;
        return [.. ListHelpers.FilterList(projects, search), .. ListHelpers.FilterList(targets, search)];
    }

    private Snapshot Build()
    {
        lock (_lock)
        {
            var stale = DateTimeOffset.UtcNow - _builtAt >= RepositoryService.CacheDuration;
            if (_snapshot is not null && !_refresh && !stale)
            {
                return _snapshot;
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
            var entries = new List<Entry>();
            var toolIndex = 0;
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
                    entries.Add(new Entry(target, item, toolIndex));
                }

                // Only the first tool may rescan the folders; the others reuse that scan.
                refresh = false;
                toolIndex++;
            }

            var snapshot = new Snapshot([.. entries.Select(entry => entry.Item)], ProjectItems(entries));
            lock (_lock)
            {
                _snapshot = snapshot;
                _builtAt = DateTimeOffset.UtcNow;
            }

            return snapshot;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private ListItem[] ProjectItems(List<Entry> entries)
    {
        var projects = Project.Group(entries.Select(entry => entry.Target.Repository).Distinct());
        return
        [
            .. projects.Select(project =>
            {
                // Per tool, the clone's targets first and then each worktree's, in the project's order.
                var items = entries
                    .Where(entry => project.Checkouts.Contains(entry.Target.Repository))
                    .OrderBy(entry => entry.ToolIndex)
                    .ThenBy(entry => IndexOf(project, entry.Target))
                    .Select(entry => entry.Item)
                    .ToArray();
                var worktrees = project.Checkouts.Count(checkout => checkout.IsWorktree);

                return new ListItem(new ProjectTargetsPage(project, ProjectTitle(project), Icon, items))
                {
                    Title = project.Name,
                    Subtitle = project.Path,
                    Icon = Icons.Repository,
                    Tags = worktrees switch
                    {
                        0 => [],
                        1 => [new Tag("1 worktree") { Icon = Icons.Worktree }],
                        _ => [new Tag($"{worktrees} worktrees") { Icon = Icons.Worktree }],
                    },
                    TextToSuggest = project.Name,
                    MoreCommands = items.Length > 0 ? items[0].MoreCommands : [],
                };
            }),
        ];
    }

    private static int IndexOf(Project project, LaunchTarget target)
    {
        for (var i = 0; i < project.Checkouts.Count; i++)
        {
            if (project.Checkouts[i] == target.Repository)
            {
                return i;
            }
        }

        return int.MaxValue;
    }

    private void Refresh()
    {
        lock (_lock)
        {
            _refresh = true;
        }

        RaiseItemsChanged();
    }

    private sealed record Entry(LaunchTarget Target, ListItem Item, int ToolIndex);

    private sealed record Snapshot(ListItem[] Targets, ListItem[] Projects);
}
