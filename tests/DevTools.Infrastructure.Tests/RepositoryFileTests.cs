using DevTools.Domain.Repositories;
using DevTools.Infrastructure.FileSystem;
using DevTools.Infrastructure.Git;
using DevTools.Infrastructure.Repositories;

namespace DevTools.Infrastructure.Tests;

public sealed class RepositoryFileTests : IDisposable
{
    private readonly TempFolder _temp = new();

    [Fact]
    public void Remotes_are_read_from_the_git_config_origin_first()
    {
        var remotes = GitConfigReader.Parse([
            "[core]",
            "\turl = not-a-remote",
            "[remote \"upstream\"]",
            "\turl = https://github.com/them/shop.git",
            "[remote \"origin\"]",
            "\turl = \"git@github.com:me/shop.git\"",
            "\turl = git@backup:me/shop.git",
            "\tfetch = +refs/heads/*:refs/remotes/origin/*",
            "[branch \"main\"]",
            "\tremote = origin",
        ]);

        Assert.Equal(["origin", "upstream"], remotes.Select(r => r.Name));
        Assert.Equal("git@github.com:me/shop.git", remotes[0].Url);
        Assert.Equal("https://github.com/me/shop", remotes[0].WebUrl);
    }

    [Fact]
    public void The_scan_finds_the_remotes_of_clones_and_their_worktrees()
    {
        var shop = _temp.Repository("repos/Shop");
        TempFolder.Git(shop, "remote", "add", "origin", "https://github.com/me/shop.git");
        TempFolder.Git(shop, "worktree", "add", "--quiet", "-b", "feature/x", _temp.Combine("worktrees", "Shop-x"));

        var found = new FileSystemRepositoryScanner().Scan([_temp.Combine("repos"), _temp.Combine("worktrees")], 3);

        Assert.Equal(2, found.Count);
        Assert.All(found, repository => Assert.Equal("https://github.com/me/shop.git", repository.PrimaryRemote?.Url));
    }

    [Fact]
    public void Repositories_json_gives_tags_actions_and_searches()
    {
        var path = _temp.File(
            "repositories.json",
            """
            {
              // comments are fine
              "tags": [
                { "tag": "work", "when": { "remote": "dev.azure.com/org", "isWorktree": false } },
                { "tag": "  " }
              ],
              "actions": [
                { "name": "Wiki", "url": "{webUrl}/wiki" },
                { "name": "Build", "command": "cmd.exe", "arguments": "/k dotnet build", "forEachFile": "*.slnx" },
                { "name": "Copy", "copy": "{remoteUrl}", "when": { "tag": "work" } },
                { "name": "Broken" },
              ],
              "searches": [{ "name": "Work", "query": "tag:work" }]
            }
            """
        );
        using var configuration = new JsonRepositoryConfiguration(path, watch: false);

        var current = configuration.Current;

        var tag = Assert.Single(current.Tags);
        Assert.Equal(("work", "dev.azure.com/org", false), (tag.Tag, tag.When.Remote, tag.When.IsWorktree));
        Assert.Equal(
            [RepositoryActionKind.Url, RepositoryActionKind.Command, RepositoryActionKind.Copy],
            current.Actions.Select(a => a.Kind)
        );
        Assert.Equal("*.slnx", current.Actions[1].ForEachFile);
        Assert.Equal("work", current.Actions[2].When.Tag);
        Assert.Equal(new SavedSearch("Work", "tag:work"), Assert.Single(current.Searches));
        Assert.Contains("Broken", configuration.ConfigurationError, StringComparison.Ordinal);
    }

    [Fact]
    public void The_example_repositories_json_reads_as_empty()
    {
        using var configuration = new JsonRepositoryConfiguration(_temp.Combine("repositories.json"), watch: false);
        configuration.EnsureConfigurationFile();

        Assert.Null(configuration.ConfigurationError);
        Assert.Empty(configuration.Current.Tags);
    }

    [Fact]
    public void A_broken_repositories_json_reports_the_error()
    {
        using var configuration = new JsonRepositoryConfiguration(
            _temp.File("repositories.json", "{ nope"),
            watch: false
        );

        Assert.Empty(configuration.Current.Actions);
        Assert.NotNull(configuration.ConfigurationError);
    }

    [Fact]
    public void The_state_survives_a_restart()
    {
        var store = new JsonRepositoryStateStore(_temp.Combine("state", "state.json"));
        var used = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
        store.Save(
            new RepositoryState
            {
                Pinned = ["/repos/shop"],
                Ignored = ["/repos/old"],
                Usage = new Dictionary<string, IReadOnlyList<DateTimeOffset>> { ["/repos/shop"] = [used] },
                SavedSearches = [new SavedSearch("Work", "tag:work")],
            }
        );

        var state = new JsonRepositoryStateStore(store.Path).Load();

        Assert.Equal(["/repos/shop"], state.Pinned);
        Assert.Equal(["/repos/old"], state.Ignored);
        Assert.Equal([used], state.Usage["/REPOS/SHOP"]);
        Assert.Equal("tag:work", Assert.Single(state.SavedSearches).Query);
    }

    [Fact]
    public void A_missing_or_broken_state_file_starts_empty()
    {
        Assert.Empty(new JsonRepositoryStateStore(_temp.Combine("none.json")).Load().Pinned);
        Assert.Empty(new JsonRepositoryStateStore(_temp.File("bad.json", "[")).Load().Pinned);
    }

    public void Dispose() => _temp.Dispose();
}
