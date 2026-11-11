using DevTools.Domain.Git;
using DevTools.Domain.Projects;

namespace DevTools.Application.Tests;

public class ProjectTests
{
    [Fact]
    public void A_clone_and_its_worktrees_are_one_project_with_the_clone_first()
    {
        var shop = TestData.Repository("Shop");
        var cart = Worktree(shop, "feature/cart");
        var api = TestData.Repository("Api");
        var bugfix = Worktree(shop, "bugfix/login");

        var projects = Project.Group([cart, api, bugfix, shop]);

        Assert.Equal(["Api", "Shop"], projects.Select(p => p.Name));
        Assert.Equal(shop.Path, projects[1].Path);
        Assert.Equal(["main", "bugfix/login", "feature/cart"], projects[1].Checkouts.Select(c => c.Head.DisplayName));
    }

    [Fact]
    public void Worktrees_whose_clone_is_not_scanned_still_form_a_project()
    {
        var shop = TestData.Repository("Shop");

        var project = Assert.Single(Project.Group([Worktree(shop, "b"), Worktree(shop, "a")]));

        Assert.Equal("Shop", project.Name);
        Assert.Equal(["a", "b"], project.Checkouts.Select(c => c.Head.DisplayName));
    }

    [Fact]
    public void Two_clones_with_the_same_name_stay_apart()
    {
        var one = TestData.Repository("Shop");
        var two = one with { Path = Path.Combine(TestData.Root, "other", "Shop") };

        Assert.Equal(2, Project.Group([one, two]).Count);
    }

    private static GitRepository Worktree(GitRepository clone, string branch) =>
        new(
            clone.Name,
            Path.Combine(TestData.Root, "worktrees", branch),
            new GitHead(branch, null),
            RepositoryKind.Worktree
        )
        {
            MainRepositoryPath = clone.Path + Path.DirectorySeparatorChar,
        };
}
