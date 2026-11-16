using DevTools.Application.Abstractions;
using DevTools.Application.Git;
using DevTools.Application.Repositories;
using DevTools.Domain.Git;
using DevTools.Domain.Settings;
using NSubstitute;

namespace DevTools.Application.Tests;

public class GitServiceTests
{
    private readonly IGitRunner _git = Substitute.For<IGitRunner>();
    private readonly GitRepository _repository = TestData.Repository();
    private readonly GitService _service;
    private readonly string _worktreeRoot = Path.Combine(TestData.Root, "wt-" + Guid.NewGuid().ToString("N"));

    public GitServiceTests()
    {
        var settings = new FakeSettings(new DevToolsSettings { WorktreeRoots = [_worktreeRoot] });
        _git.RunAsync(default!, default!).ReturnsForAnyArgs(new GitResult(0, "", ""));
        _service = new GitService(
            _git,
            settings,
            new RepositoryService(Substitute.For<IRepositoryScanner>(), settings, TimeProvider.System)
        );
    }

    [Fact]
    public async Task A_remote_branch_is_checked_out_as_a_tracking_branch()
    {
        await _service.CheckoutAsync(_repository, new GitBranch("origin/feature/x", IsRemote: true, IsCurrent: false));

        await _git.Received(1)
            .RunAsync(
                _repository.Path,
                Arg.Is<IReadOnlyList<string>>(a => a.SequenceEqual(new[] { "switch", "--track", "origin/feature/x" }))
            );
    }

    [Fact]
    public async Task A_worktree_for_a_remote_branch_gets_a_local_tracking_branch()
    {
        var result = await _service.AddWorktreeAsync(
            _repository,
            new GitBranch("origin/feature/x", IsRemote: true, IsCurrent: false)
        );

        var path = Path.Combine(_worktreeRoot, "Shop", "feature-x");
        Assert.Equal(path, result.Path);
        await _git.Received(1)
            .RunAsync(
                _repository.Path,
                Arg.Is<IReadOnlyList<string>>(a =>
                    a.SequenceEqual(new[] { "worktree", "add", "--track", "-b", "feature/x", path, "origin/feature/x" })
                )
            );
    }

    [Fact]
    public async Task A_new_branch_in_a_new_worktree()
    {
        var result = await _service.AddWorktreeForNewBranchAsync(_repository, "feature/new");

        Assert.True(result.Succeeded);
        await _git.Received(1)
            .RunAsync(
                _repository.Path,
                Arg.Is<IReadOnlyList<string>>(a =>
                    a.SequenceEqual(
                        new[]
                        {
                            "worktree",
                            "add",
                            "-b",
                            "feature/new",
                            Path.Combine(_worktreeRoot, "Shop", "feature-new"),
                        }
                    )
                )
            );
    }

    [Fact]
    public async Task An_invalid_branch_name_never_reaches_git()
    {
        var result = await _service.CreateBranchAsync(_repository, "not valid");

        Assert.False(result.Succeeded);
        await _git.DidNotReceiveWithAnyArgs().RunAsync(default!, default!);
    }

    [Fact]
    public async Task The_main_worktree_is_never_removed()
    {
        var result = await _service.RemoveWorktreeAsync(
            _repository,
            new GitWorktree(_repository.Path, GitHead.Unknown, IsMain: true)
        );

        Assert.False(result.Succeeded);
        await _git.DidNotReceiveWithAnyArgs().RunAsync(default!, default!);
    }

    [Fact]
    public async Task Remote_branches_with_a_local_twin_are_hidden()
    {
        _git.RunAsync(_repository.Path, Arg.Is<IReadOnlyList<string>>(a => a[0] == "for-each-ref"))
            .Returns(
                new GitResult(
                    0,
                    "refs/heads/main\0*\0\nrefs/remotes/origin/main\0 \0\nrefs/remotes/origin/feature\0 \0\n",
                    ""
                )
            );

        var branches = await _service.ListBranchesAsync(_repository);

        Assert.Equal(["main", "origin/feature"], branches.Select(b => b.Name));
    }
}
