using System.Runtime.CompilerServices;
using DevTools.Application.Abstractions;
using DevTools.Domain.Git;
using DevTools.Domain.Projects;
using DevTools.Domain.Repositories;

namespace DevTools.Application.Repositories;

/// <summary>
/// The repositories as the lists show them: each with its tags (from <c>repositories.json</c>), pin and usage, pinned
/// ones first and the rest in the order chosen in the settings.
/// </summary>
public sealed class RepositoryCatalog
{
    private readonly RepositoryService _repositories;
    private readonly RepositoryPreferences _preferences;
    private readonly IRepositoryConfiguration _configuration;
    private readonly ISettingsProvider _settings;
    private readonly Lock _lock = new();
    private ConditionalWeakTable<GitRepository, IReadOnlyList<string>> _tags = [];

    public RepositoryCatalog(
        RepositoryService repositories,
        RepositoryPreferences preferences,
        IRepositoryConfiguration configuration,
        ISettingsProvider settings
    )
    {
        _repositories = repositories;
        _preferences = preferences;
        _configuration = configuration;
        _settings = settings;
        _configuration.Changed += (_, _) =>
        {
            lock (_lock)
            {
                _tags = [];
            }
        };
    }

    /// <summary>Every repository that isn't ignored, sorted.</summary>
    public IReadOnlyList<RepositoryEntry> GetEntries(bool refresh = false) =>
        Sort([.. _repositories.GetRepositories(refresh).Select(GetEntry)]);

    public RepositoryEntry GetEntry(GitRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return new RepositoryEntry(
            repository,
            TagsFor(repository),
            _preferences.IsPinned(repository.Path),
            _preferences.GetUsage(repository.Path)
        );
    }

    /// <summary>
    /// The tags of a repository, in the order <c>repositories.json</c> defines them. Worked out once per scan (a
    /// condition may look for a file).
    /// </summary>
    public IReadOnlyList<string> TagsFor(GitRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ConditionalWeakTable<GitRepository, IReadOnlyList<string>> table;
        lock (_lock)
        {
            table = _tags;
        }

        return table.GetValue(
            repository,
            r =>
                [
                    .. _configuration
                        .Current.Tags.Where(rule =>
                            !string.IsNullOrWhiteSpace(rule.Tag) && RepositoryConditions.Matches(rule.When, r)
                        )
                        .Select(rule => rule.Tag.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase),
                ]
        );
    }

    /// <summary>Pinned first, then by the order from the settings; the name breaks ties.</summary>
    public IReadOnlyList<RepositoryEntry> Sort(IReadOnlyList<RepositoryEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var ordered = entries.OrderByDescending(entry => entry.IsPinned);
        ordered = _settings.Current.Order switch
        {
            RepositoryOrder.RecentlyUsed => ordered.ThenByDescending(entry =>
                entry.Usage.LastUsed ?? DateTimeOffset.MinValue
            ),
            RepositoryOrder.MostUsed => ordered
                .ThenByDescending(entry => entry.Usage.Count)
                .ThenByDescending(entry => entry.Usage.LastUsed ?? DateTimeOffset.MinValue),
            _ => ordered,
        };

        return
        [
            .. ordered
                .ThenBy(entry => entry.Repository.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.Repository.Kind)
                .ThenBy(entry => entry.Repository.Head.DisplayName, StringComparer.OrdinalIgnoreCase),
        ];
    }

    /// <summary>
    /// Projects in the same order: a project counts as pinned when one of its checkouts is, and as used when one of
    /// them was.
    /// </summary>
    public IReadOnlyList<Project> Sort(IReadOnlyList<Project> projects)
    {
        ArgumentNullException.ThrowIfNull(projects);
        var summaries = projects
            .Select(project =>
            {
                var entries = project.Checkouts.Select(GetEntry).ToArray();
                return (
                    Project: project,
                    Pinned: entries.Any(e => e.IsPinned),
                    Count: entries.Sum(e => e.Usage.Count),
                    Last: entries.Max(e => e.Usage.LastUsed) ?? DateTimeOffset.MinValue
                );
            })
            .OrderByDescending(s => s.Pinned);

        summaries = _settings.Current.Order switch
        {
            RepositoryOrder.RecentlyUsed => summaries.ThenByDescending(s => s.Last),
            RepositoryOrder.MostUsed => summaries.ThenByDescending(s => s.Count).ThenByDescending(s => s.Last),
            _ => summaries,
        };

        return
        [
            .. summaries
                .ThenBy(s => s.Project.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.Project.Path, StringComparer.OrdinalIgnoreCase)
                .Select(s => s.Project),
        ];
    }
}
