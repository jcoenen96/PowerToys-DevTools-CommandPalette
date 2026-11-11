using DevTools.Application.Repositories;
using DevTools.Domain.Git;
using DevTools.Domain.Projects;
using DevTools.Domain.Settings;

namespace DevTools.Application.Tests;

public class LaunchTargetFilterTests
{
    private static readonly LaunchTarget Clone = Target(RepositoryKind.Clone);
    private static readonly LaunchTarget Worktree = Target(RepositoryKind.Worktree);

    [Fact]
    public void Worktrees_wait_for_a_search_by_default()
    {
        var settings = new DevToolsSettings();

        Assert.True(LaunchTargetFilter.IsVisible(Clone, "", settings));
        Assert.False(LaunchTargetFilter.IsVisible(Worktree, "", settings));
        Assert.False(LaunchTargetFilter.IsVisible(Worktree, "   ", settings));
        Assert.True(LaunchTargetFilter.IsVisible(Worktree, "shop", settings));
    }

    [Fact]
    public void With_the_setting_off_worktrees_always_show()
    {
        var settings = new DevToolsSettings { ShowWorktreesOnlyWhenSearching = false };

        Assert.True(LaunchTargetFilter.IsVisible(Worktree, "", settings));
        Assert.True(LaunchTargetFilter.IsVisible(Worktree, "shop", settings));
    }

    private static LaunchTarget Target(RepositoryKind kind)
    {
        var repository = TestData.Repository(kind: kind);
        return new LaunchTarget(TestData.Code, repository, repository.Path, IsFolder: true);
    }
}
