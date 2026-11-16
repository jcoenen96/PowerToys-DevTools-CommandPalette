using DevTools.Application.Git;
using DevTools.Application.Repositories;
using DevTools.Application.Search;
using DevTools.Domain.Git;
using DevTools.Domain.Repositories;

namespace DevTools.Application.Tests;

public class RepositoryQueryTests
{
    private static readonly RepositoryEntry Shop = Entry(
        TestData.Repository("Shop", "feature/cart") with
        {
            Remotes = [new GitRemote("origin", "git@github.com:me/shop.git")],
        },
        pinned: true,
        "work",
        "dotnet"
    );

    private static readonly RepositoryEntry Api = Entry(TestData.Repository("Api"), pinned: false, "private");

    private static readonly GitStatus Dirty = new("origin/main", Ahead: 0, Behind: 2, Changed: 3, Untracked: 0);

    [Theory]
    [InlineData("tag:work", true)]
    [InlineData("tag:private", false)]
    [InlineData("-tag:work", false)]
    [InlineData("!tag:private", true)]
    [InlineData("tag:wo*", true)]
    [InlineData("is:pinned", true)]
    [InlineData("is:favorite", true)]
    [InlineData("is:worktree", false)]
    [InlineData("is:clone", true)]
    [InlineData("branch:feature", true)]
    [InlineData("branch:main", false)]
    [InlineData("name:sh", true)]
    [InlineData("remote:github.com", true)]
    [InlineData("remote:origin", true)]
    [InlineData("has:remote", true)]
    [InlineData("tag:work tag:dotnet", true)]
    [InlineData("tag:work tag:private", false)]
    [InlineData("tag:private OR tag:dotnet", true)]
    [InlineData("tag:private | branch:main", false)]
    [InlineData("TAG:WORK", true)]
    public void Filters_match_tags_pins_branches_and_remotes(string query, bool matches) =>
        Assert.Equal(matches, RepositoryQuery.Parse(query).Matches(Shop, status: null));

    [Theory]
    [InlineData("has:changes", true)]
    [InlineData("is:behind", true)]
    [InlineData("is:ahead", false)]
    [InlineData("is:clean", false)]
    [InlineData("has:upstream", true)]
    public void Status_filters_use_git_status(string query, bool matches)
    {
        var parsed = RepositoryQuery.Parse(query);

        Assert.True(parsed.NeedsStatus);
        Assert.Equal(matches, parsed.Matches(Shop, Dirty));
        Assert.False(parsed.Matches(Shop, status: null) && matches);
    }

    [Fact]
    public void Free_text_is_left_for_the_fuzzy_search()
    {
        var query = RepositoryQuery.Parse("shop tag:work cart");

        Assert.Equal("shop cart", query.FreeText);
        Assert.True(query.HasFilters);
        Assert.False(query.NeedsStatus);
    }

    [Fact]
    public void Words_that_are_not_filters_stay_free_text()
    {
        var query = RepositoryQuery.Parse("C:\\repos foo:bar -x");

        Assert.Equal("C:\\repos foo:bar -x", query.FreeText);
        Assert.False(query.HasFilters);
        Assert.True(query.Matches(Api, status: null));
    }

    [Fact]
    public void Quotes_keep_spaces_in_a_value()
    {
        var entry = Entry(new GitRepository("x", "/home/me/my repos/x", GitHead.Unknown, RepositoryKind.Clone), false);

        Assert.True(RepositoryQuery.Parse("path:\"my repos\"").Matches(entry, null));
    }

    [Fact]
    public void A_free_text_word_that_is_a_tag_finds_the_tag()
    {
        Assert.True(RepositoryQuery.Parse("work").FreeTextMatchesTag(Shop));
        Assert.False(RepositoryQuery.Parse("work").FreeTextMatchesTag(Api));
    }

    [Fact]
    public void An_empty_query_matches_everything()
    {
        Assert.True(RepositoryQuery.Parse("").Matches(Api, null));
        Assert.True(RepositoryQuery.Parse("  ").Matches(Shop, null));
    }

    private static RepositoryEntry Entry(GitRepository repository, bool pinned, params string[] tags) =>
        new(repository, tags, pinned, RepositoryUsage.None);
}
