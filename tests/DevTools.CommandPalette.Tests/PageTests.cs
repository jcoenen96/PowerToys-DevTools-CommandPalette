using DevTools.CommandPalette.Pages;
using DevTools.Domain.Repositories;
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

        Assert.Equal(["Status", "Git", "Branches and worktrees", "Remote", "Open", "Repository"], sections);
    }

    [Fact]
    public void The_git_page_browses_the_remote_and_starts_a_pull_request()
    {
        var shop = _test.Services.Repositories.GetRepositories().Single(r => r is { Name: "Shop", IsWorktree: false });

        var remote = new RepositoryPage(_test.Services, shop).GetItems().Where(item => item.Section == "Remote");

        Assert.Equal(
            ["Browse repository", "Browse main on remote", "Create pull request for main", "Copy remote URL"],
            remote.Select(item => item.Title)
        );
        Assert.Equal("https://github.com/me/shop/compare/main?expand=1", remote.ElementAt(2).Subtitle);
    }

    [Fact]
    public void A_pinned_project_comes_first()
    {
        var shop = _test.Services.Repositories.GetRepositories().Single(r => r is { Name: "Shop", IsWorktree: false });
        _test.Services.Preferences.SetPinned(shop.Path, true);

        var projects = new ToolTargetsPage(_test.Services, TestServices.SolutionTool).GetItems();

        Assert.Equal(["Shop", "Api"], projects.Select(item => item.Title));
        Assert.Contains(projects[0].Tags, tag => tag.Text == "pinned");
    }

    [Fact]
    public void Tags_from_repositories_json_show_and_filter_the_tool_pages()
    {
        _test.Configuration.Set(
            new RepositoryConfiguration { Tags = [new TagRule("backend", new RepositoryCondition { Name = "Api" })] }
        );
        var page = new ToolTargetsPage(_test.Services, TestServices.FolderTool);

        var api = page.GetItems().Single(item => item.Title == "Api");
        page.SearchText = "tag:backend";
        var filtered = page.GetItems().Select(item => item.Title).ToArray();
        page.SearchText = "-tag:backend shop";
        var excluded = page.GetItems().Select(item => item.Title).ToArray();

        Assert.Contains(api.Tags, tag => tag.Text == "backend");
        Assert.Equal(["Api", "Api (main)"], filtered);
        Assert.DoesNotContain(excluded, title => title.StartsWith("Api", StringComparison.Ordinal));
        Assert.Contains("Shop", excluded);
    }

    [Fact]
    public void An_ignored_repository_is_hidden_until_shown_again()
    {
        var api = _test.Services.Repositories.GetRepositories().Single(r => r.Name == "Api");

        _test.Services.Preferences.SetIgnored(api.Path, true);
        var hidden = new ToolTargetsPage(_test.Services, TestServices.FolderTool).GetItems().Select(item => item.Title);
        var ignored = new IgnoredRepositoriesPage(_test.Services).GetItems();

        Assert.Equal(["Shop"], hidden);
        Assert.Equal("Api", ignored[0].Title);
        Assert.Contains(new RepositoriesPage(_test.Services).GetItems(), item => item.Title == "Ignored repositories");
    }

    [Fact]
    public void The_git_repositories_page_filters_and_offers_to_save_a_search()
    {
        var shop = _test.Services.Repositories.GetRepositories().Single(r => r is { Name: "Shop", IsWorktree: false });
        _test.Services.Preferences.SetPinned(shop.Path, true);
        var page = new RepositoriesPage(_test.Services) { SearchText = "is:pinned" };

        var items = page.GetItems();

        Assert.Equal(["Shop (main)", "Save the search \"is:pinned\""], items.Select(item => item.Title));
        Assert.Contains(page.Filters!.GetFilters().OfType<IFilter>(), filter => filter.Id == "is:pinned");
    }

    [Fact]
    public void Custom_actions_show_in_the_context_menu()
    {
        _test.Configuration.Set(
            new RepositoryConfiguration
            {
                Actions =
                [
                    new RepositoryAction
                    {
                        Name = "Wiki",
                        Kind = RepositoryActionKind.Url,
                        Url = "{webUrl}/wiki",
                        When = new RepositoryCondition { Remote = "github.com" },
                    },
                ],
            }
        );

        var items = new RepositoriesPage(_test.Services).GetItems();
        var menu = (IListItem item) => item.MoreCommands.OfType<ICommandContextItem>().Select(c => c.Title).ToArray();

        Assert.Contains("Wiki", menu(items.Single(item => item.Title == "Shop (main)")));
        Assert.DoesNotContain("Wiki", menu(items.Single(item => item.Title == "Api (main)")));
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
