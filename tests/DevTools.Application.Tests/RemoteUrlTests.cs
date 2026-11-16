using DevTools.Domain.Git;

namespace DevTools.Application.Tests;

public class RemoteUrlTests
{
    [Theory]
    [InlineData("https://github.com/me/shop.git", "https://github.com/me/shop")]
    [InlineData("git@github.com:me/shop.git", "https://github.com/me/shop")]
    [InlineData("ssh://git@github.com/me/shop.git", "https://github.com/me/shop")]
    [InlineData("ssh://git@gitlab.example.com:2222/team/shop.git", "https://gitlab.example.com/team/shop")]
    [InlineData("https://me@dev.azure.com/org/project/_git/shop", "https://dev.azure.com/org/project/_git/shop")]
    [InlineData("git@ssh.dev.azure.com:v3/org/project/shop", "https://dev.azure.com/org/project/_git/shop")]
    [InlineData("org@vs-ssh.visualstudio.com:v3/org/project/shop", "https://org.visualstudio.com/project/_git/shop")]
    [InlineData("http://localhost:3000/me/shop.git", "https://localhost:3000/me/shop")]
    [InlineData("C:\\repos\\shop", null)]
    [InlineData("/srv/git/shop.git", null)]
    [InlineData("file:///srv/git/shop.git", null)]
    [InlineData("", null)]
    public void A_remote_url_becomes_its_web_page(string remote, string? web) =>
        Assert.Equal(web, RemoteUrls.ToWebUrl(remote));

    [Theory]
    [InlineData("https://github.com/me/shop", "https://github.com/me/shop/compare/feature/cart?expand=1")]
    [InlineData(
        "https://gitlab.com/me/shop",
        "https://gitlab.com/me/shop/-/merge_requests/new?merge_request%5Bsource_branch%5D=feature%2Fcart"
    )]
    [InlineData(
        "https://dev.azure.com/org/project/_git/shop",
        "https://dev.azure.com/org/project/_git/shop/pullrequestcreate?sourceRef=feature%2Fcart"
    )]
    [InlineData(
        "https://bitbucket.org/me/shop",
        "https://bitbucket.org/me/shop/pull-requests/new?source=feature%2Fcart"
    )]
    public void A_pull_request_starts_on_the_hosts_own_page(string web, string expected) =>
        Assert.Equal(expected, RemoteUrls.PullRequestUrl(web, "feature/cart"));

    [Theory]
    [InlineData("https://github.com/me/shop", "https://github.com/me/shop/tree/feature/cart")]
    [InlineData("https://gitlab.com/me/shop", "https://gitlab.com/me/shop/-/tree/feature/cart")]
    [InlineData(
        "https://dev.azure.com/org/p/_git/shop",
        "https://dev.azure.com/org/p/_git/shop?version=GBfeature%2Fcart"
    )]
    public void A_branch_has_a_page_on_the_remote(string web, string expected) =>
        Assert.Equal(expected, RemoteUrls.BranchUrl(web, "feature/cart"));
}
