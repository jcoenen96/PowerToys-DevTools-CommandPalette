using DevTools.Application.Git;
using DevTools.Domain.Settings;

namespace DevTools.Application.Tests;

public class WorktreePathBuilderTests
{
    private static readonly string WorktreeRoot = Path.Combine(TestData.Root, "worktrees");

    [Fact]
    public void New_worktrees_go_into_the_first_worktree_folder()
    {
        var settings = new DevToolsSettings
        {
            WorktreeRoots = ["", WorktreeRoot, Path.Combine(TestData.Root, "other")],
        };

        var path = WorktreePathBuilder.Build(settings, TestData.Repository(), "feature/cart");

        Assert.Equal(Path.Combine(WorktreeRoot, "Shop", "feature-cart"), path);
    }

    [Fact]
    public void The_template_decides_the_layout()
    {
        var settings = new DevToolsSettings
        {
            WorktreeRoots = [WorktreeRoot],
            WorktreePathTemplate = "{worktreeRoot}\\{repo}.{branch}",
        };

        var path = WorktreePathBuilder.Build(settings, TestData.Repository(), "fix/login");

        Assert.Equal(Path.Combine(WorktreeRoot, "Shop.fix-login"), path);
    }

    [Fact]
    public void Without_a_worktree_folder_there_is_no_path() =>
        Assert.Null(WorktreePathBuilder.Build(new DevToolsSettings(), TestData.Repository(), "main"));
}
