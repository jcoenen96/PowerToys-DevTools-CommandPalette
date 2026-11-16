using DevTools.Application.Abstractions;
using DevTools.Application.Repositories;
using DevTools.Domain.Git;
using DevTools.Domain.Repositories;
using DevTools.Domain.Settings;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace DevTools.Application.Tests;

public class RepositoryPreferenceTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero));
    private readonly MemoryStore _store = new();
    private readonly RepositoryPreferences _preferences;
    private readonly GitRepository _shop = TestData.Repository("Shop");
    private readonly GitRepository _api = TestData.Repository("Api");
    private readonly GitRepository _web = TestData.Repository("Web");

    public RepositoryPreferenceTests() => _preferences = new RepositoryPreferences(_store, _time);

    [Fact]
    public void Pins_are_saved_and_toggle()
    {
        Assert.True(_preferences.TogglePin(_shop.Path));
        Assert.True(new RepositoryPreferences(_store, _time).IsPinned(_shop.Path + Path.DirectorySeparatorChar));

        Assert.False(_preferences.TogglePin(_shop.Path));
        Assert.False(_preferences.IsPinned(_shop.Path));
    }

    [Fact]
    public void Ignored_repositories_are_left_out_of_the_list_until_shown_again()
    {
        var scanner = Substitute.For<IRepositoryScanner>();
        scanner.Scan(default!, default).ReturnsForAnyArgs([_shop, _api]);
        var repositories = new RepositoryService(scanner, new FakeSettings(), _time, _preferences);
        _preferences.SetPinned(_api.Path, true);

        _preferences.SetIgnored(_api.Path, true);

        Assert.Equal(new[] { _shop }, repositories.GetRepositories());
        Assert.Equal(2, repositories.GetAllRepositories().Count);
        Assert.False(_preferences.IsPinned(_api.Path));

        _preferences.ClearIgnored();
        Assert.Equal(2, repositories.GetRepositories().Count);
    }

    [Fact]
    public void Usage_counts_the_last_30_days()
    {
        _preferences.RecordUse(_shop.Path);
        _time.Advance(TimeSpan.FromDays(31));
        _preferences.RecordUse(_shop.Path);
        _preferences.RecordUse(_shop.Path);

        var usage = _preferences.GetUsage(_shop.Path);

        Assert.Equal(2, usage.Count);
        Assert.Equal(_time.GetUtcNow(), usage.LastUsed);
        Assert.Equal(RepositoryUsage.None, _preferences.GetUsage(_api.Path));
    }

    [Fact]
    public void Saving_the_same_search_twice_keeps_one()
    {
        _preferences.SaveSearch("Work", "tag:work");
        _preferences.SaveSearch("Work repositories", "tag:work");
        _preferences.SaveSearch("", "has:changes");

        Assert.Equal(
            [new SavedSearch("Work repositories", "tag:work"), new SavedSearch("has:changes", "has:changes")],
            _preferences.SavedSearches
        );

        _preferences.DeleteSearch(_preferences.SavedSearches[0]);
        Assert.Single(_preferences.SavedSearches);
    }

    [Theory]
    [InlineData(RepositoryOrder.Name, new[] { "Shop", "Api", "Web" })]
    [InlineData(RepositoryOrder.RecentlyUsed, new[] { "Shop", "Api", "Web" })]
    [InlineData(RepositoryOrder.MostUsed, new[] { "Shop", "Web", "Api" })]
    public void Pinned_repositories_come_first_then_the_chosen_order(RepositoryOrder order, string[] expected)
    {
        var catalog = Catalog(new DevToolsSettings { Order = order });
        _preferences.SetPinned(_shop.Path, true);
        _preferences.RecordUse(_web.Path);
        _preferences.RecordUse(_web.Path);
        _time.Advance(TimeSpan.FromMinutes(1));
        _preferences.RecordUse(_api.Path);

        var names = catalog.GetEntries().Select(entry => entry.Repository.Name);

        Assert.Equal(expected, names);
    }

    [Fact]
    public void Tags_come_from_the_rules_that_match()
    {
        var configuration = new FakeConfiguration
        {
            Current = new RepositoryConfiguration
            {
                Tags =
                [
                    new TagRule("work", new RepositoryCondition { Name = "S*" }),
                    new TagRule("api", new RepositoryCondition { Name = "api" }),
                    new TagRule("everything", RepositoryCondition.Always),
                    new TagRule("worktrees", new RepositoryCondition { IsWorktree = true }),
                ],
            },
        };
        var catalog = Catalog(new DevToolsSettings(), configuration);

        Assert.Equal(["work", "everything"], catalog.TagsFor(_shop));
        Assert.Equal(["api", "everything"], catalog.TagsFor(_api));
    }

    private RepositoryCatalog Catalog(DevToolsSettings settings, IRepositoryConfiguration? configuration = null)
    {
        var scanner = Substitute.For<IRepositoryScanner>();
        scanner.Scan(default!, default).ReturnsForAnyArgs([_shop, _api, _web]);
        var fake = new FakeSettings(settings);
        return new RepositoryCatalog(
            new RepositoryService(scanner, fake, _time, _preferences),
            _preferences,
            configuration ?? new FakeConfiguration(),
            fake
        );
    }

    private sealed class MemoryStore : IRepositoryStateStore
    {
        private RepositoryState _state = RepositoryState.Empty;

        public RepositoryState Load() => _state;

        public void Save(RepositoryState state) => _state = state;
    }
}

internal sealed class FakeConfiguration : IRepositoryConfiguration
{
    public string ConfigurationPath => "repositories.json";

    public RepositoryConfiguration Current { get; set; } = RepositoryConfiguration.Empty;

    public string? ConfigurationError => null;

    public void EnsureConfigurationFile() { }

    public event EventHandler? Changed
    {
        add { }
        remove { }
    }
}
