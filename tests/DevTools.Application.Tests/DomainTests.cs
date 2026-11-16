using DevTools.Domain.Git;
using DevTools.Domain.Projects;

namespace DevTools.Application.Tests;

public class DomainTests
{
    [Theory]
    [InlineData("main", true)]
    [InlineData("feature/login", true)]
    [InlineData("fix-123", true)]
    [InlineData("", false)]
    [InlineData("-leading-dash", false)]
    [InlineData("has space", false)]
    [InlineData("double..dot", false)]
    [InlineData("ends/", false)]
    [InlineData("ends.lock", false)]
    [InlineData("feature/.hidden", false)]
    [InlineData("what?", false)]
    [InlineData("@", false)]
    public void Branch_names_follow_the_git_rules(string name, bool valid) =>
        Assert.Equal(valid, BranchNames.IsValid(name));

    [Theory]
    [InlineData("feature/login", "feature-login")]
    [InlineData("users/joris/fix", "users-joris-fix")]
    [InlineData("main", "main")]
    public void Branches_become_folder_names(string branch, string folder) =>
        Assert.Equal(folder, BranchNames.ToFolderName(branch));

    [Fact]
    public void A_detached_head_shows_the_short_commit() =>
        Assert.Equal("detached@1a2b3c4", new GitHead(null, "1a2b3c4d5e6f").DisplayName);

    [Fact]
    public void A_solution_shows_the_branch_behind_its_name()
    {
        var repository = TestData.Repository(branch: "feature/cart");
        var target = new LaunchTarget(
            TestData.VisualStudio,
            repository,
            Path.Combine(repository.Path, "Shop.slnx"),
            IsFolder: false
        );

        Assert.Equal("Shop.slnx (feature/cart)", target.Title);
        Assert.Equal(repository.Path, target.Folder);
    }

    [Fact]
    public void A_folder_target_is_named_after_the_repository()
    {
        var repository = TestData.Repository("Api", "develop", RepositoryKind.Worktree);
        var target = new LaunchTarget(TestData.Code, repository, repository.Path, IsFolder: true);

        Assert.Equal("Api (develop)", target.Title);
    }

    [Fact]
    public void A_remote_branch_knows_its_local_name() =>
        Assert.Equal("feature/x", new GitBranch("origin/feature/x", IsRemote: true, IsCurrent: false).LocalName);
}
