using DevTools.Application.Abstractions;
using DevTools.Application.Git;
using DevTools.Application.Repositories;
using DevTools.Application.Tools;
using DevTools.Domain.Git;
using DevTools.Domain.Projects;
using DevTools.Domain.Settings;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace DevTools.Application.Tests;

public class ServiceTests
{
    private readonly IRepositoryScanner _scanner = Substitute.For<IRepositoryScanner>();
    private readonly IFileFinder _files = Substitute.For<IFileFinder>();
    private readonly FakeTimeProvider _time = new();
    private readonly FakeSettings _settings = new(
        new DevToolsSettings { RepositoryRoots = ["/repos"], WorktreeRoots = ["/worktrees"] }
    );

    [Fact]
    public void Repositories_come_from_both_the_repository_and_the_worktree_folders()
    {
        var repositories = new RepositoryService(_scanner, _settings, _time);

        repositories.GetRepositories();

        _scanner
            .Received(1)
            .Scan(Arg.Is<IReadOnlyList<string>>(roots => roots.SequenceEqual(new[] { "/repos", "/worktrees" })), 3);
    }

    [Fact]
    public void A_scan_is_reused_for_a_while_and_redone_after_a_settings_change()
    {
        _scanner.Scan(default!, default).ReturnsForAnyArgs([TestData.Repository()]);
        var repositories = new RepositoryService(_scanner, _settings, _time);

        repositories.GetRepositories();
        repositories.GetRepositories();
        _scanner.ReceivedWithAnyArgs(1).Scan(default!, default);

        _time.Advance(RepositoryService.CacheDuration);
        repositories.GetRepositories();
        _scanner.ReceivedWithAnyArgs(2).Scan(default!, default);

        _settings.Set(_settings.Current with { ScanDepth = 5 });
        repositories.GetRepositories();
        _scanner.ReceivedWithAnyArgs(3).Scan(default!, default);
    }

    [Fact]
    public void Clones_are_listed_before_their_worktrees()
    {
        _scanner
            .Scan(default!, default)
            .ReturnsForAnyArgs([
                TestData.Repository("Shop", "feature/b", RepositoryKind.Worktree) with
                {
                    Path = Path.Combine(TestData.Root, "wt", "b"),
                },
                TestData.Repository("Api"),
                TestData.Repository("Shop"),
            ]);

        var result = new RepositoryService(_scanner, _settings, _time).GetRepositories();

        Assert.Equal(["Api main", "Shop main", "Shop feature/b"], result.Select(r => $"{r.Name} {r.Head.DisplayName}"));
    }

    [Fact]
    public void A_solution_tool_lists_the_slnx_and_falls_back_to_the_folder()
    {
        var shop = TestData.Repository("Shop");
        var empty = TestData.Repository("Docs");
        _scanner.Scan(default!, default).ReturnsForAnyArgs([shop, empty]);
        _files
            .Find(shop.Path, TestData.VisualStudio.FilePatterns, 3)
            .Returns([
                Path.Combine(shop.Path, "tools", "Tools.sln"),
                Path.Combine(shop.Path, "Shop.sln"),
                Path.Combine(shop.Path, "Shop.slnx"),
            ]);
        _files.Find(empty.Path, Arg.Any<IReadOnlyList<string>>(), 3).Returns([]);
        var targets = new LaunchTargetService(new RepositoryService(_scanner, _settings, _time), _files, _settings);

        var result = targets.GetTargets(TestData.VisualStudio);

        Assert.Equal(
            ["Docs (main)", "Shop.slnx (main)", "Tools.sln (main)"],
            result.Select(t => t.Title).Order(StringComparer.Ordinal)
        );
        Assert.True(result.Single(t => t.Repository == empty).IsFolder);
    }

    [Fact]
    public void A_folder_tool_opens_every_repository_once()
    {
        _scanner.Scan(default!, default).ReturnsForAnyArgs([TestData.Repository("Shop"), TestData.Repository("Api")]);
        var targets = new LaunchTargetService(new RepositoryService(_scanner, _settings, _time), _files, _settings);

        var result = targets.GetTargets(TestData.Code);

        Assert.All(result, target => Assert.True(target.IsFolder));
        Assert.Equal(2, result.Count);
        _files.DidNotReceiveWithAnyArgs().Find(default!, default!, default);
    }

    [Fact]
    public void Launching_renders_the_arguments_and_starts_in_the_target_folder()
    {
        var resolver = Substitute.For<IExecutableResolver>();
        var process = Substitute.For<IProcessLauncher>();
        resolver.Resolve(TestData.VisualStudio.Executables).Returns("C:/VS/devenv.exe");
        var repository = TestData.Repository();
        var solution = Path.Combine(repository.Path, "Shop.slnx");

        var result = new ToolLauncher(resolver, process).Launch(
            new LaunchTarget(TestData.VisualStudio, repository, solution, IsFolder: false)
        );

        Assert.True(result.Succeeded);
        process.Received(1).Start("C:/VS/devenv.exe", $"\"{solution}\"", repository.Path);
    }

    [Fact]
    public void Launching_a_tool_that_is_not_installed_explains_what_to_do()
    {
        var result = new ToolLauncher(Substitute.For<IExecutableResolver>(), Substitute.For<IProcessLauncher>()).Launch(
            new LaunchTarget(TestData.Code, TestData.Repository(), TestData.Repository().Path, IsFolder: true)
        );

        Assert.False(result.Succeeded);
        Assert.Contains("tools.json", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Switched_off_tools_are_left_out()
    {
        var catalog = Substitute.For<IToolCatalog>();
        catalog.GetTools().Returns([TestData.VisualStudio, TestData.Code]);
        _settings.Set(
            _settings.Current with
            {
                DisabledToolIds = new HashSet<string>(["VSCODE"], StringComparer.OrdinalIgnoreCase),
            }
        );

        var tools = new ToolService(catalog, _settings, Substitute.For<IExecutableResolver>()).GetEnabledTools();

        Assert.Equal([TestData.VisualStudio], tools);
    }
}
