using DevTools.Domain.Git;

namespace DevTools.Application.Git;

/// <summary>Parses the machine-readable output of the git commands <see cref="GitService"/> runs.</summary>
internal static class GitOutputParser
{
    public const string BranchFormat = "%(refname)%00%(HEAD)%00%(worktreepath)";

    /// <summary>Parses <c>git for-each-ref --format=<see cref="BranchFormat"/> refs/heads refs/remotes</c>.</summary>
    public static IReadOnlyList<GitBranch> ParseBranches(string output)
    {
        var branches = new List<GitBranch>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.TrimEnd('\r').Split('\0');
            var reference = fields[0];
            var isCurrent = fields.Length > 1 && fields[1] == "*";
            var worktree = fields.Length > 2 && fields[2].Length > 0 ? fields[2] : null;

            if (reference.StartsWith("refs/heads/", StringComparison.Ordinal))
            {
                branches.Add(
                    new GitBranch(reference["refs/heads/".Length..], false, isCurrent) { WorktreePath = worktree }
                );
            }
            else if (
                reference.StartsWith("refs/remotes/", StringComparison.Ordinal)
                && !reference.EndsWith("/HEAD", StringComparison.Ordinal)
            )
            {
                branches.Add(new GitBranch(reference["refs/remotes/".Length..], true, false));
            }
        }

        return branches;
    }

    /// <summary>Parses <c>git worktree list --porcelain</c>; the first entry is the main worktree.</summary>
    public static IReadOnlyList<GitWorktree> ParseWorktrees(string output)
    {
        var worktrees = new List<GitWorktree>();
        string? path = null,
            commit = null,
            branch = null;
        bool prunable = false,
            locked = false,
            bare = false;

        void Flush()
        {
            if (path is not null && !bare)
            {
                worktrees.Add(
                    new GitWorktree(path, new GitHead(branch, commit), worktrees.Count == 0)
                    {
                        IsPrunable = prunable,
                        IsLocked = locked,
                    }
                );
            }

            path = commit = branch = null;
            prunable = locked = bare = false;
        }

        foreach (var raw in output.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0)
            {
                Flush();
            }
            else if (line.StartsWith("worktree ", StringComparison.Ordinal))
            {
                Flush();
                path = line["worktree ".Length..];
            }
            else if (line.StartsWith("HEAD ", StringComparison.Ordinal))
            {
                commit = line["HEAD ".Length..];
            }
            else if (line.StartsWith("branch ", StringComparison.Ordinal))
            {
                branch = line["branch ".Length..].Replace("refs/heads/", "", StringComparison.Ordinal);
            }
            else if (line == "bare")
            {
                bare = true;
            }
            else if (line.StartsWith("prunable", StringComparison.Ordinal))
            {
                prunable = true;
            }
            else if (line.StartsWith("locked", StringComparison.Ordinal))
            {
                locked = true;
            }
        }

        Flush();
        return worktrees;
    }
}
