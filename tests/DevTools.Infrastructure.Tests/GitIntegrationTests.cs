using DevTools.Application.Abstractions;
using DevTools.Application.Git;
using DevTools.Application.Repositories;
using DevTools.Domain.Git;
using DevTools.Domain.Settings;
using DevTools.Infrastructure.FileSystem;
using DevTools.Infrastructure.Git;

namespace DevTools.Infrastructure.Tests;

/// <summary>The git use cases against real repositories, through the real git executable.</summary>
public sealed class GitIntegrationTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private readonly RepositoryService _repositories;
    private readonly GitService _git;

    public GitIntegrationTests()
    {
        _temp.Repository("repos/Shop");
        var settings = new StaticSettings(
            new DevToolsSettings
            {
                RepositoryRoots = [_temp.Combine("repos")],
                WorktreeRoots = [_temp.Combine("worktrees")],
            }
        );
        _repositories = new RepositoryService(new FileSystemRepositoryScanner(), settings, TimeProvider.System);
        _git = new GitService(new GitProcessRunner(settings), settings, _repositories);
    }

    [Fact]
    public async Task A_new_branch_in_a_new_worktree_shows_up_as_a_repository()
    {
        var shop = _repositories.GetRepositories().Single();

        var result = await _git.AddWorktreeForNewBranchAsync(shop, "feature/cart");

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(_temp.Combine("worktrees", "Shop", "feature-cart"), result.Path);
        var worktree = _repositories.GetRepositories().Single(r => r.IsWorktree);
        Assert.Equal(("Shop", "feature/cart"), (worktree.Name, worktree.Head.Branch));

        var worktrees = await _git.ListWorktreesAsync(shop);
        Assert.Equal(2, worktrees.Count);

        var removed = await _git.RemoveWorktreeAsync(shop, worktrees.Single(w => !w.IsMain));
        Assert.True(removed.Succeeded, removed.Summary);
        Assert.DoesNotContain(_repositories.GetRepositories(), r => r.IsWorktree);
    }

    [Fact]
    public async Task A_worktree_is_grouped_with_its_clone_into_one_project()
    {
        var shop = _repositories.GetRepositories().Single();
        Assert.True((await _git.AddWorktreeForNewBranchAsync(shop, "feature/cart")).Succeeded);

        var project = Assert.Single(Domain.Projects.Project.Group(_repositories.GetRepositories()));

        Assert.Equal("Shop", project.Name);
        Assert.Equal([RepositoryKind.Clone, RepositoryKind.Worktree], project.Checkouts.Select(c => c.Kind));
        Assert.Equal(["main", "feature/cart"], project.Checkouts.Select(c => c.Head.DisplayName));
    }

    [Fact]
    public async Task Branches_are_created_listed_and_checked_out()
    {
        var shop = _repositories.GetRepositories().Single();

        Assert.True((await _git.CreateBranchAsync(shop, "feature/a")).Succeeded);
        Assert.Equal("feature/a", _repositories.GetRepositories().Single().Head.Branch);

        var branches = await _git.ListBranchesAsync(shop);
        Assert.Equal(["feature/a", "main"], branches.Select(b => b.Name));
        Assert.True(branches[0].IsCurrent);

        Assert.True((await _git.CheckoutAsync(shop, branches[1])).Succeeded);
        Assert.Equal("main", _repositories.GetRepositories().Single().Head.Branch);
    }

    [Fact]
    public async Task Status_reports_untracked_files()
    {
        var shop = _repositories.GetRepositories().Single();
        _temp.File("repos/Shop/new.txt", "x");

        var status = await _git.GetStatusAsync(shop);

        Assert.Equal(1, status?.Untracked);
    }

    [Fact]
    public async Task A_failing_command_reports_gits_error()
    {
        var shop = _repositories.GetRepositories().Single();

        var result = await _git.RunAsync(shop, GitCommands.Pull);

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Summary);
    }

    public void Dispose() => _temp.Dispose();

    private sealed class StaticSettings(DevToolsSettings settings) : ISettingsProvider
    {
        public DevToolsSettings Current => settings;

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }
    }
}
