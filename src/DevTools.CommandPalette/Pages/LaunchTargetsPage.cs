using DevTools.Application.Repositories;
using DevTools.Application.Search;
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
    private bool _stale;

    protected LaunchTargetsPage(DevToolsServices services)
    {
        Services = services;
        EmptyContent = EmptyStates.NoRepositories(services);

        // A pin, an ignored repository or a use changes the order and the tags; the scan itself is still good.
        services.Preferences.Changed += (_, _) => MarkStale();
        services.RepositoryConfiguration.Changed += (_, _) => MarkStale();
    }

    protected DevToolsServices Services { get; }

    /// <summary>The tools whose targets this page lists, in the order they are shown.</summary>
    protected abstract IEnumerable<ToolDefinition> Tools(IReadOnlyList<ToolDefinition> installed);

    /// <summary>Lets a page add the tool to each item, e.g. a section per tool.</summary>
    protected virtual void Decorate(ListItem item, LaunchTarget target) { }

    /// <summary>The icon of a project on the first list: the tool's on a tool page.</summary>
    protected abstract IconInfo ProjectIcon { get; }

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
        var searching = !string.IsNullOrWhiteSpace(SearchText);

        // Filters such as tag:work or is:pinned narrow the list; the rest of the text is matched as before.
        var query = RepositoryQuery.Parse(SearchText);
        var search = query.FreeText;
        var targets = snapshot.Targets.Where(entry => query.Matches(entry.Repository, status: null)).ToArray();

        if (!Services.Settings.Current.GroupByProject)
        {
            return searching ? [.. Find(targets, query)] : [.. targets.Select(entry => entry.Item)];
        }

        if (!searching)
        {
            return [.. snapshot.Projects.Select(project => project.Item)];
        }

        var projects = snapshot.Projects.Where(project => project.Checkouts.Any(c => query.Matches(c, status: null)));
        return [.. Find(projects, query), .. Find(targets, query)];
    }

    /// <summary>
    /// The items whose name or path fuzzily match the free text, after the ones with a tag that is that text. Without
    /// free text (only filters), all of them.
    /// </summary>
    private static IEnumerable<IListItem> Find(IEnumerable<IMatchable> candidates, RepositoryQuery query)
    {
        var all = candidates.ToArray();
        if (query.FreeText.Length == 0)
        {
            return all.Select(candidate => candidate.Item);
        }

        var tagged = all.Where(candidate => candidate.Checkouts.Any(query.FreeTextMatchesTag)).ToArray();
        return
        [
            .. tagged.Select(candidate => candidate.Item),
            .. ListHelpers.FilterList(all.Except(tagged).Select(candidate => candidate.Item), query.FreeText),
        ];
    }

    private Snapshot Build()
    {
        lock (_lock)
        {
            var stale = _stale || DateTimeOffset.UtcNow - _builtAt >= RepositoryService.CacheDuration;
            if (_snapshot is not null && !_refresh && !stale)
            {
                return _snapshot;
            }

            _stale = false;
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
            var catalog = Services.RepositoryCatalog;
            var entries = new List<Entry>();
            var toolIndex = 0;
            foreach (var tool in Tools(installed))
            {
                // Every result shows the icon of the program it opens with; the worktree tag tells them apart.
                var icon = Icons.For(tool, Services.Resolver);
                foreach (var target in Services.Targets.GetTargets(tool, refresh))
                {
                    var repository = catalog.GetEntry(target.Repository);
                    var item = new ListItem(new LaunchCommand(Services, target))
                    {
                        Title = target.Title,
                        Subtitle = target.Path,
                        Icon = icon,
                        Tags = Tags.For(repository),
                        TextToSuggest = target.Name,
                        MoreCommands = RepositoryMenu.For(Services, target.Repository, installed, Refresh, target),
                    };
                    Decorate(item, target);
                    entries.Add(new Entry(target, repository, item, toolIndex));
                }

                // Only the first tool may rescan the folders; the others reuse that scan.
                refresh = false;
                toolIndex++;
            }

            // Pinned repositories first, then the order from the settings; per tool, as the tools are listed.
            var order = catalog
                .Sort([.. entries.Select(entry => entry.Repository).DistinctBy(entry => entry.Repository)])
                .Select((entry, index) => (entry.Repository, index))
                .ToDictionary(pair => pair.Repository, pair => pair.index);
            var sorted = entries
                .OrderBy(entry => entry.ToolIndex)
                .ThenBy(entry => order[entry.Target.Repository])
                .ToList();

            var snapshot = new Snapshot([.. sorted], ProjectItems(sorted));
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

    private ProjectEntry[] ProjectItems(List<Entry> entries)
    {
        var catalog = Services.RepositoryCatalog;
        var projects = catalog.Sort(Project.Group(entries.Select(entry => entry.Target.Repository).Distinct()));
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
                var checkouts = project.Checkouts.Select(catalog.GetEntry).ToArray();

                ITag[] worktreeTags = worktrees switch
                {
                    0 => [],
                    1 => [new Tag("1 worktree") { Icon = Icons.Worktree }],
                    _ => [new Tag($"{worktrees} worktrees") { Icon = Icons.Worktree }],
                };

                var item = new ListItem(new ProjectTargetsPage(project, ProjectTitle(project), Icon, items))
                {
                    Title = project.Name,
                    Subtitle = project.Path,
                    Icon = ProjectIcon,
                    // Whether one of its checkouts is pinned, and the tags of all of them.
                    Tags =
                    [
                        .. Tags.For(
                            checkouts.Any(checkout => checkout.IsPinned),
                            checkouts.SelectMany(checkout => checkout.Tags)
                        ),
                        .. worktreeTags,
                    ],
                    TextToSuggest = project.Name,
                    MoreCommands = items.Length > 0 ? items[0].MoreCommands : [],
                };
                return new ProjectEntry(item, checkouts);
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

    private void MarkStale()
    {
        lock (_lock)
        {
            _stale = true;
        }

        RaiseItemsChanged();
    }

    /// <summary>Something the search can match: an item and the checkouts behind it.</summary>
    private interface IMatchable
    {
        ListItem Item { get; }

        IReadOnlyList<RepositoryEntry> Checkouts { get; }
    }

    private sealed record Entry(LaunchTarget Target, RepositoryEntry Repository, ListItem Item, int ToolIndex)
        : IMatchable
    {
        public IReadOnlyList<RepositoryEntry> Checkouts => [Repository];
    }

    private sealed record ProjectEntry(ListItem Item, IReadOnlyList<RepositoryEntry> Checkouts) : IMatchable;

    private sealed record Snapshot(Entry[] Targets, ProjectEntry[] Projects);
}
