using DevTools.Application.Repositories;
using DevTools.Application.Search;
using DevTools.CommandPalette.Commands;
using DevTools.Domain.Repositories;
using DevTools.Infrastructure;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DevTools.CommandPalette.Pages;

/// <summary>
/// All clones and worktrees with their branch, tags and git status (ahead, behind, changed); choosing one opens its
/// git commands. Pinned repositories come first. The search box takes filters (<c>tag:work</c>, <c>is:pinned</c>,
/// <c>has:changes</c>, <c>-tag:archived</c>, see <see cref="RepositoryQuery"/>), the filter list next to it offers
/// the common ones, every tag and the saved searches, and a search can be saved.
/// </summary>
internal sealed partial class RepositoriesPage : DynamicListPage
{
    private readonly DevToolsServices _services;
    private readonly SavedSearch? _savedSearch;
    private readonly RepositoryFilters _filters;
    private readonly Dictionary<SavedSearch, RepositoriesPage> _searchPages = [];
    private bool _refresh;

    /// <param name="services">The services.</param>
    /// <param name="savedSearch">A saved search the page always applies, with its name as the title.</param>
    public RepositoriesPage(DevToolsServices services, SavedSearch? savedSearch = null)
    {
        _services = services;
        _savedSearch = savedSearch;
        Name = "Open";
        Title = savedSearch?.Name ?? "Git repositories";
        Icon = savedSearch is null ? Icons.Repository : Icons.Search;
        PlaceholderText = "Search repositories and branches, or filter: tag:work is:pinned has:changes is:behind";
        EmptyContent = EmptyStates.NoRepositories(services);
        if (savedSearch is null)
        {
            Id = CommandIds.Repositories;
        }

        _filters = new RepositoryFilters(services);
        _filters.PropChanged += (_, _) => RaiseItemsChanged();
        Filters = _filters;

        // Pages for saved searches are made once (see SearchPage), so these handlers don't pile up.
        _services.Status.Updated += (_, _) => RaiseItemsChanged();
        _services.Preferences.Changed += (_, _) => RaiseItemsChanged();
        _services.Repositories.Invalidated += (_, _) => RaiseItemsChanged();
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        if (!string.Equals(oldSearch, newSearch, StringComparison.Ordinal))
        {
            RaiseItemsChanged();
        }
    }

    public override IListItem[] GetItems()
    {
        IsLoading = true;
        try
        {
            var refresh = _refresh;
            _refresh = false;
            var installed = _services.Tools.GetInstalledTools();
            var search = RepositoryQuery.Parse(SearchText);
            var filter = RepositoryQuery.Parse(RepositoryFilters.QueryOf(_filters.CurrentFilterId));
            var saved = RepositoryQuery.Parse(_savedSearch?.Query);
            var entries = _services.RepositoryCatalog.GetEntries(refresh);

            var settings = _services.Settings.Current;
            if (settings.ShowGitStatus || search.NeedsStatus || filter.NeedsStatus || saved.NeedsStatus)
            {
                // Lists what is known now; the counts appear once git status is done (Status.Updated).
                _ = _services.Status.RefreshAsync(entries.Select(entry => entry.Repository));
            }

            var matching = entries
                .Where(entry =>
                {
                    var status = _services.Status.Get(entry.Repository);
                    return saved.Matches(entry, status)
                        && filter.Matches(entry, status)
                        && search.Matches(entry, status);
                })
                .ToArray();

            var items = matching.Select(entry => (Entry: entry, Item: Item(entry, installed))).ToArray();
            var searching = !string.IsNullOrWhiteSpace(SearchText);
            if (!searching)
            {
                return [.. SavedSearchItems(), .. items.Select(pair => pair.Item), .. FooterItems()];
            }

            IListItem[] found =
                search.FreeText.Length == 0
                    ? [.. items.Select(pair => pair.Item)]
                    :
                    [
                        // A word that is a tag finds the repositories with that tag, like in RepoM.
                        .. items.Where(pair => search.FreeTextMatchesTag(pair.Entry)).Select(pair => pair.Item),
                        .. ListHelpers.FilterList(
                            items.Where(pair => !search.FreeTextMatchesTag(pair.Entry)).Select(pair => pair.Item),
                            search.FreeText
                        ),
                    ];

            return [.. found, SaveSearchItem(SearchText.Trim())];
        }
        finally
        {
            IsLoading = false;
        }
    }

    private ListItem Item(RepositoryEntry entry, IReadOnlyList<Domain.Tools.ToolDefinition> installed)
    {
        var repository = entry.Repository;
        var status = _services.Settings.Current.ShowGitStatus ? _services.Status.Get(repository) : null;
        return new ListItem(new RepositoryPage(_services, repository))
        {
            Title = $"{repository.Name} ({repository.Head.DisplayName})",
            Subtitle = repository.Path,
            Icon = Icons.For(repository),
            Tags = Tags.For(entry, status),
            Section = entry.IsPinned ? "Pinned" : "Repositories",
            MoreCommands = RepositoryMenu.For(_services, repository, installed, Refresh),
        };
    }

    /// <summary>The searches from repositories.json and the ones saved here, each opening its own list.</summary>
    private IEnumerable<ListItem> SavedSearchItems()
    {
        if (_savedSearch is not null)
        {
            yield break;
        }

        foreach (var search in _services.RepositoryConfiguration.Current.Searches)
        {
            yield return new ListItem(SearchPage(search))
            {
                Title = search.Name,
                Subtitle = search.Query,
                Icon = Icons.Search,
                Section = "Saved searches",
            };
        }

        foreach (var search in _services.Preferences.SavedSearches)
        {
            yield return new ListItem(SearchPage(search))
            {
                Title = search.Name,
                Subtitle = search.Query,
                Icon = Icons.Search,
                Section = "Saved searches",
                MoreCommands = [new CommandContextItem(new DeleteSearchCommand(_services, search, Refresh))],
            };
        }
    }

    private IEnumerable<ListItem> FooterItems()
    {
        if (_savedSearch is not null)
        {
            yield break;
        }

        yield return new ListItem(
            new BackgroundCommand(
                "Fetch all",
                Icons.Fetch,
                "Fetching every repository…",
                async () =>
                {
                    var round = await _services.AutoFetch.FetchAllAsync().ConfigureAwait(false);
                    return round switch
                    {
                        { AlreadyRunning: true } => (true, "A fetch of every repository is already running."),
                        { Failed: 0 } => (true, $"Fetched {round.Fetched} repositories"),
                        _ => (false, $"Fetched {round.Fetched} repositories; {round.Failed} failed"),
                    };
                }
            )
        )
        {
            Title = "Fetch all repositories",
            Subtitle = "git fetch --all in every repository with a remote, once per clone and its worktrees",
            Icon = Icons.Fetch,
            Section = "All repositories",
        };

        var ignored = _services.Preferences.IgnoredPaths.Count;
        if (ignored > 0)
        {
            yield return new ListItem(new IgnoredRepositoriesPage(_services))
            {
                Title = "Ignored repositories",
                Subtitle = ignored == 1 ? "1 repository is hidden" : $"{ignored} repositories are hidden",
                Icon = Icons.Hide,
                Section = "All repositories",
            };
        }
    }

    private RepositoriesPage SearchPage(SavedSearch search)
    {
        lock (_searchPages)
        {
            if (!_searchPages.TryGetValue(search, out var page))
            {
                page = new RepositoriesPage(_services, search);
                _searchPages[search] = page;
            }

            return page;
        }
    }

    private ListItem SaveSearchItem(string query) =>
        new(new SaveSearchCommand(_services, query, Refresh))
        {
            Title = $"Save the search \"{query}\"",
            Subtitle = "Lists it on this page and in the filters",
            Icon = Icons.Save,
            Section = "Search",
        };

    private void Refresh()
    {
        _refresh = true;
        RaiseItemsChanged();
    }
}

/// <summary>
/// The quick filters next to the search box, like RepoM's: everything, pinned, with changes, ahead, behind,
/// worktrees, one per tag, and the saved searches. A filter's id is its query.
/// </summary>
internal sealed partial class RepositoryFilters : Filters
{
    private readonly DevToolsServices _services;

    private const string AllId = "all";

    public RepositoryFilters(DevToolsServices services)
    {
        _services = services;
        CurrentFilterId = AllId;
    }

    /// <summary>The query behind a filter id: the id itself, or nothing for "All repositories".</summary>
    public static string QueryOf(string? id) => id is null or AllId ? "" : id;

    public override IFilterItem[] GetFilters()
    {
        var filters = new List<IFilterItem>
        {
            Filter(AllId, "All repositories", Icons.Repository),
            Filter("is:pinned", "Pinned", Icons.Pin),
            Filter("has:changes", "With changes", Icons.Edit),
            Filter("is:ahead", "Ahead (to push)", Icons.Branch),
            Filter("is:behind", "Behind (to pull)", Icons.Fetch),
            Filter("is:worktree", "Worktrees", Icons.Worktree),
        };

        var tags = _services
            .RepositoryCatalog.GetEntries()
            .SelectMany(entry => entry.Tags)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (tags.Length > 0)
        {
            filters.Add(new Separator("Tags"));
            filters.AddRange(tags.Select(tag => Filter($"tag:{Quote(tag)}", tag, Icons.Tag)));
        }

        var searches = _services
            .RepositoryConfiguration.Current.Searches.Concat(_services.Preferences.SavedSearches)
            .DistinctBy(search => search.Query, StringComparer.Ordinal)
            .ToArray();
        if (searches.Length > 0)
        {
            filters.Add(new Separator("Saved searches"));
            filters.AddRange(searches.Select(search => Filter(search.Query, search.Name, Icons.Search)));
        }

        return [.. filters];
    }

    private static Filter Filter(string id, string name, IconInfo icon) =>
        new()
        {
            Id = id,
            Name = name,
            Icon = icon,
        };

    private static string Quote(string tag) => tag.Contains(' ', StringComparison.Ordinal) ? $"\"{tag}\"" : tag;
}
