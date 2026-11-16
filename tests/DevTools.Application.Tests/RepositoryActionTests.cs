using DevTools.Application.Abstractions;
using DevTools.Application.Git;
using DevTools.Application.Repositories;
using DevTools.Domain.Git;
using DevTools.Domain.Repositories;
using DevTools.Domain.Settings;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace DevTools.Application.Tests;

public class RepositoryActionTests
{
    private readonly IFileFinder _files = Substitute.For<IFileFinder>();
    private readonly IProcessLauncher _launcher = Substitute.For<IProcessLauncher>();
    private readonly IExecutableResolver _resolver = Substitute.For<IExecutableResolver>();
    private readonly FakeConfiguration _configuration = new();

    private readonly GitRepository _shop = TestData.Repository("Shop", "feature/cart") with
    {
        Remotes = [new GitRemote("origin", "git@github.com:me/shop.git")],
    };

    [Fact]
    public void Placeholders_are_filled_in()
    {
        var text = RepositoryActionService.Render("{name} {branch} {webUrl}/wiki {remoteUrl} {path}", _shop);

        Assert.Equal(
            $"Shop feature/cart https://github.com/me/shop/wiki git@github.com:me/shop.git {_shop.Path}",
            text
        );
    }

    [Fact]
    public void Actions_only_show_where_their_condition_matches()
    {
        _configuration.Current = new RepositoryConfiguration
        {
            Actions =
            [
                new RepositoryAction
                {
                    Name = "Wiki",
                    Url = "{webUrl}/wiki",
                    When = new() { Remote = "github.com" },
                },
                new RepositoryAction
                {
                    Name = "Boards",
                    Url = "{webUrl}/boards",
                    When = new() { Remote = "azure" },
                },
                new RepositoryAction
                {
                    Name = "Work only",
                    Kind = RepositoryActionKind.Copy,
                    Text = "x",
                    When = new() { Tag = "work" },
                },
                new RepositoryAction
                {
                    Name = "Copy",
                    Kind = RepositoryActionKind.Copy,
                    Text = "{remoteUrl}",
                },
            ],
        };

        var actions = Service().For(Entry(_shop));

        Assert.Equal(["Wiki", "Copy"], actions.Select(action => action.Name));
        Assert.Equal("https://github.com/me/shop/wiki", actions[0].Target);
        Assert.Equal("git@github.com:me/shop.git", actions[1].Target);
    }

    [Fact]
    public void A_url_action_without_a_remote_is_left_out()
    {
        _configuration.Current = new RepositoryConfiguration
        {
            Actions = [new RepositoryAction { Name = "Wiki", Url = "{webUrl}/wiki" }],
        };

        Assert.Empty(Service().For(Entry(TestData.Repository("Local"))));
    }

    [Fact]
    public void For_each_file_repeats_the_action_per_file()
    {
        var tests = Path.Combine(_shop.Path, "tests", "Shop.Tests.csproj");
        var more = Path.Combine(_shop.Path, "tests", "Api.Tests.csproj");
        _files
            .Find(_shop.Path, Arg.Is<IReadOnlyList<string>>(p => p[0] == "*.Tests.csproj"), Arg.Any<int>())
            .Returns([tests, more]);
        _configuration.Current = new RepositoryConfiguration
        {
            Actions =
            [
                new RepositoryAction
                {
                    Name = "dotnet test {fileName}",
                    Kind = RepositoryActionKind.Command,
                    Executable = "cmd.exe",
                    Arguments = "/k dotnet test \"{file}\"",
                    ForEachFile = "*.Tests.csproj",
                },
            ],
        };

        var actions = Service().For(Entry(_shop));

        Assert.Equal(["dotnet test Api.Tests", "dotnet test Shop.Tests"], actions.Select(action => action.Name));
        Assert.Equal($"/k dotnet test \"{more}\"", actions[0].Arguments);
        Assert.Equal(_shop.Path, actions[0].WorkingDirectory);
    }

    [Fact]
    public void A_command_starts_the_resolved_executable_in_the_repository()
    {
        _resolver.Resolve(Arg.Any<IReadOnlyList<string>>()).Returns("C:\\Windows\\System32\\cmd.exe");
        var action = new ResolvedRepositoryAction(
            "Build",
            RepositoryActionKind.Command,
            "cmd.exe",
            "/k dotnet build",
            _shop.Path,
            null
        );

        var result = Service().Run(action);

        Assert.True(result.Succeeded);
        _launcher.Received(1).Start("C:\\Windows\\System32\\cmd.exe", "/k dotnet build", _shop.Path);
    }

    [Fact]
    public void Auto_fetch_fetches_each_project_once()
    {
        var clone = _shop;
        var worktree = TestData.Repository("Shop", "feature/x", RepositoryKind.Worktree) with
        {
            Path = Path.Combine(TestData.Root, "worktrees", "Shop-x"),
            MainRepositoryPath = _shop.Path,
            Remotes = _shop.Remotes,
        };
        var local = TestData.Repository("Local");

        var fetched = AutoFetchService.OnePerProject([worktree, clone, local]).ToArray();

        Assert.Equal([clone], fetched);
    }

    [Fact]
    public async Task Auto_fetch_runs_on_its_interval_and_prunes_when_asked()
    {
        var time = new FakeTimeProvider();
        var git = Substitute.For<IGitRunner>();
        git.RunAsync(default!, default!).ReturnsForAnyArgs(new GitResult(0, "", ""));
        var settings = new FakeSettings(
            new DevToolsSettings { AutoFetchInterval = TimeSpan.FromMinutes(15), PruneOnFetch = true }
        );
        var scanner = Substitute.For<IRepositoryScanner>();
        scanner.Scan(default!, default).ReturnsForAnyArgs([_shop]);
        var repositories = new RepositoryService(scanner, settings, time);
        using var autoFetch = new AutoFetchService(
            new GitService(git, settings, repositories),
            repositories,
            settings,
            time
        );
        var round = new TaskCompletionSource<FetchRound>();
        autoFetch.Fetched += (_, r) => round.TrySetResult(r);

        time.Advance(TimeSpan.FromMinutes(15));
        var result = await round.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(1, result.Fetched);
        await git.Received(1)
            .RunAsync(
                _shop.Path,
                Arg.Is<IReadOnlyList<string>>(a => a.SequenceEqual(new[] { "fetch", "--all", "--prune", "--quiet" })),
                Arg.Any<CancellationToken>()
            );

        settings.Set(new DevToolsSettings { AutoFetchInterval = TimeSpan.Zero });
        Assert.Equal(TimeSpan.Zero, autoFetch.Interval);
    }

    [Fact]
    public async Task Status_is_read_once_and_forgotten_after_a_command()
    {
        var git = Substitute.For<IGitRunner>();
        git.RunAsync(default!, default!)
            .ReturnsForAnyArgs(
                new GitResult(0, "# branch.ab +1 -2\n1 .M N... 100644 100644 100644 a b file.txt\n", "")
            );
        var settings = new FakeSettings();
        var service = new GitService(
            git,
            settings,
            new RepositoryService(Substitute.For<IRepositoryScanner>(), settings, TimeProvider.System)
        );
        var cache = new GitStatusCache(service, TimeProvider.System);
        var updates = 0;
        cache.Updated += (_, _) => updates++;

        await cache.RefreshAsync([_shop]);
        await cache.RefreshAsync([_shop]);

        Assert.Equal(1, updates);
        Assert.Equal((1, 2, 1), (cache.Get(_shop)!.Ahead, cache.Get(_shop)!.Behind, cache.Get(_shop)!.Changed));

        await service.RunAsync(_shop, GitCommands.Pull);
        Assert.True(cache.IsStale(_shop));
        Assert.Null(cache.Get(_shop));
    }

    [Theory]
    [InlineData("Shop", "Sh*", true)]
    [InlineData("Shop", "s?op", true)]
    [InlineData("Shop", "*op", true)]
    [InlineData("Shop", "Api*", false)]
    [InlineData("feature/cart", "feature/*", true)]
    [InlineData("main", "feature/*", false)]
    public void Wildcards_ignore_case(string text, string pattern, bool matches) =>
        Assert.Equal(matches, Wildcard.IsMatch(text, pattern));

    private RepositoryActionService Service() => new(_configuration, _files, new FakeSettings(), _resolver, _launcher);

    private static RepositoryEntry Entry(GitRepository repository) => new(repository, [], false, RepositoryUsage.None);
}
