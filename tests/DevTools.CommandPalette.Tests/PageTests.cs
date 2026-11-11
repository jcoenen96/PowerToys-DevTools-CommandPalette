using DevTools.CommandPalette.Pages;
using Microsoft.CommandPalette.Extensions;

namespace DevTools.CommandPalette.Tests;

/// <summary>The real pages, driven the way Command Palette drives them: list, open an item, type to search.</summary>
public sealed class PageTests : IDisposable
{
    private readonly TestServices _test = new();

    [Fact]
    public void A_tool_page_lists_the_projects_and_a_project_lists_its_clone_before_its_worktrees()
    {
        var page = new ToolTargetsPage(_test.Services, TestServices.SolutionTool);

        var projects = page.GetItems();

        Assert.Equal(["Api", "Shop"], projects.Select(item => item.Title));
        var shop = Assert.IsType<ProjectTargetsPage>(projects[1].Command);
        Assert.Equal(["Shop.slnx (main)", "Shop.slnx (feature/cart)"], shop.GetItems().Select(item => item.Title));
    }

    [Fact]
    public void Every_result_shows_the_icon_of_the_program_it_opens_with()
    {
        var page = new ToolTargetsPage(_test.Services, TestServices.FolderTool);
        var program = page.Icon.Dark.Icon;
        page.SearchText = "shop";

        Assert.EndsWith("cmd.exe", program, StringComparison.OrdinalIgnoreCase);
        Assert.All(page.GetItems(), item => Assert.Equal(program, item.Icon?.Dark.Icon));
    }

    [Theory]
    [InlineData("shop")]
    [InlineData("cart")]
    [InlineData("feature/cart")]
    [InlineData("zzz")]
    [InlineData("\\")]
    [InlineData("é")]
    public void Searching_a_tool_page_does_not_throw(string search)
    {
        var page = new ToolTargetsPage(_test.Services, TestServices.SolutionTool);
        page.GetItems();

        page.SearchText = search;
        var results = page.GetItems();

        if (search == "cart")
        {
            Assert.Contains(results, item => item.Title == "Shop.slnx (feature/cart)");
        }
    }

    [Fact]
    public void Search_finds_projects_first_and_then_their_branches()
    {
        var page = new ToolTargetsPage(_test.Services, TestServices.FolderTool) { SearchText = "shop" };

        var results = page.GetItems().Select(item => item.Title).ToArray();

        Assert.Equal("Shop", results[0]);
        Assert.Contains("Shop (feature/cart)", results);
    }

    [Fact]
    public void With_projects_first_off_every_clone_and_worktree_is_listed_right_away()
    {
        _test.Settings.Current = _test.Settings.Current with { GroupByProject = false };

        var results = new ToolTargetsPage(_test.Services, TestServices.FolderTool).GetItems();

        Assert.Equal(3, results.Length);
    }

    [Fact]
    public void All_dev_tools_lists_and_searches()
    {
        var page = new AllToolsPage(_test.Services);

        var shop = page.GetItems().Single(item => item.Title == "Shop");
        var items = Assert.IsType<ProjectTargetsPage>(shop.Command).GetItems();
        page.SearchText = "Shop";

        Assert.NotEmpty(page.GetItems());
        Assert.Equal(["Solution tool", "Solution tool", "Folder tool", "Folder tool"], items.Select(i => i.Section));
    }

    [Fact]
    public void The_git_page_lists_git_commands_before_open_in()
    {
        var shop = _test.Services.Repositories.GetRepositories().Single(r => r is { Name: "Shop", IsWorktree: false });

        var sections = new RepositoryPage(_test.Services, shop).GetItems().Select(item => item.Section).Distinct();

        Assert.Equal(["Status", "Git", "Branches and worktrees", "Open"], sections);
    }

    [Fact]
    public void The_git_context_menu_starts_with_git_commands()
    {
        var page = new RepositoriesPage(_test.Services);

        var menu = page.GetItems()[0].MoreCommands.OfType<ICommandContextItem>().Select(item => item.Title).ToArray();

        Assert.Equal("Switch branch", menu[0]);
        Assert.True(
            Array.FindIndex(menu, title => title.StartsWith("Push", StringComparison.Ordinal))
                < Array.FindIndex(menu, title => title.StartsWith("Open", StringComparison.Ordinal))
        );
    }

    public void Dispose() => _test.Dispose();
}
