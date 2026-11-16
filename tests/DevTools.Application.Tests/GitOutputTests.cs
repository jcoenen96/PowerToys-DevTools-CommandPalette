using DevTools.Application.Git;

namespace DevTools.Application.Tests;

public class GitOutputTests
{
    [Fact]
    public void Branches_are_parsed_with_the_current_one_and_their_worktrees()
    {
        var output = string.Join(
            '\n',
            "refs/heads/main\0*\0/repos/Shop",
            "refs/heads/feature/cart\0 \0/worktrees/Shop/feature-cart",
            "refs/heads/old\0 \0",
            "refs/remotes/origin/HEAD\0 \0",
            "refs/remotes/origin/main\0 \0",
            "refs/remotes/origin/feature/remote-only\0 \0"
        );

        var branches = GitOutputParser.ParseBranches(output);

        Assert.Collection(
            branches,
            b => Assert.Equal(("main", false, true, "/repos/Shop"), (b.Name, b.IsRemote, b.IsCurrent, b.WorktreePath)),
            b => Assert.Equal(("feature/cart", "/worktrees/Shop/feature-cart"), (b.Name, b.WorktreePath)),
            b => Assert.Equal(("old", (string?)null), (b.Name, b.WorktreePath)),
            b => Assert.Equal(("origin/main", true), (b.Name, b.IsRemote)),
            b => Assert.Equal(("origin/feature/remote-only", "feature/remote-only"), (b.Name, b.LocalName))
        );
    }

    [Fact]
    public void Worktrees_are_parsed_with_the_main_one_first()
    {
        const string output = """
            worktree /repos/Shop
            HEAD 1111111111111111111111111111111111111111
            branch refs/heads/main

            worktree /worktrees/Shop/feature-cart
            HEAD 2222222222222222222222222222222222222222
            branch refs/heads/feature/cart
            locked

            worktree /worktrees/Shop/gone
            HEAD 3333333333333333333333333333333333333333
            detached
            prunable gitdir file points to non-existent location

            """;

        var worktrees = GitOutputParser.ParseWorktrees(output);

        Assert.Equal(3, worktrees.Count);
        Assert.True(worktrees[0].IsMain);
        Assert.Equal("main", worktrees[0].Head.Branch);
        Assert.Equal(("feature/cart", true), (worktrees[1].Head.Branch, worktrees[1].IsLocked));
        Assert.True(worktrees[2].Head.IsDetached);
        Assert.True(worktrees[2].IsPrunable);
    }

    [Fact]
    public void Status_counts_changes_and_ahead_behind()
    {
        const string output = """
            # branch.oid 1111111111111111111111111111111111111111
            # branch.head main
            # branch.upstream origin/main
            # branch.ab +2 -1
            1 .M N... 100644 100644 100644 abc abc src/Program.cs
            2 R. N... 100644 100644 100644 abc abc R100 new.cs	old.cs
            ? notes.txt
            """;

        var status = GitStatus.Parse(output);

        Assert.Equal(new GitStatus("origin/main", 2, 1, 2, 1), status);
        Assert.Equal("↑2 · ↓1 · 2 changed · 1 untracked", status.Summary);
    }

    [Fact]
    public void A_clean_repository_says_so() => Assert.Equal("clean", GitStatus.Parse("# branch.head main\n").Summary);
}
