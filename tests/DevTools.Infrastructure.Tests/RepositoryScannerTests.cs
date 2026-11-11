using DevTools.Domain.Git;
using DevTools.Infrastructure.FileSystem;
using DevTools.Infrastructure.Git;

namespace DevTools.Infrastructure.Tests;

public sealed class RepositoryScannerTests : IDisposable
{
    private readonly TempFolder _temp = new();

    [Fact]
    public void Clones_and_worktrees_are_found_with_their_branches()
    {
        var shop = _temp.Repository("repos/work/Shop");
        _temp.Repository("repos/Api");
        var worktree = _temp.Combine("worktrees", "Shop", "feature-cart");
        TempFolder.Git(shop, "worktree", "add", "-b", "feature/cart", worktree);

        var found = new FileSystemRepositoryScanner().Scan([_temp.Combine("repos"), _temp.Combine("worktrees")], 3);

        Assert.Equal(
            ["Api main Clone", "Shop feature/cart Worktree", "Shop main Clone"],
            found.Select(r => $"{r.Name} {r.Head.DisplayName} {r.Kind}").Order(StringComparer.Ordinal)
        );
        var linked = found.Single(r => r.IsWorktree);
        Assert.Equal(worktree, linked.Path);
        Assert.Equal(shop, linked.MainRepositoryPath);
    }

    [Fact]
    public void The_scan_stops_at_the_configured_depth()
    {
        _temp.Repository("a/b/Deep");

        Assert.Empty(new FileSystemRepositoryScanner().Scan([_temp.Path], 2));
        Assert.Single(new FileSystemRepositoryScanner().Scan([_temp.Path], 3));
    }

    [Fact]
    public void Missing_roots_are_skipped() =>
        Assert.Empty(new FileSystemRepositoryScanner().Scan([_temp.Combine("does-not-exist")], 3));

    [Fact]
    public void A_detached_head_is_read_as_a_commit()
    {
        var repository = _temp.Repository("Detached");
        var commit = TempFolder.Git(repository, "rev-parse", "HEAD");
        TempFolder.Git(repository, "checkout", "--quiet", "--detach");

        var head = GitHeadReader.Read(repository)!.Head;

        Assert.Equal(new GitHead(null, commit), head);
    }

    [Fact]
    public void A_folder_without_git_is_not_a_repository() => Assert.Null(GitHeadReader.Read(_temp.Path));

    public void Dispose() => _temp.Dispose();
}
