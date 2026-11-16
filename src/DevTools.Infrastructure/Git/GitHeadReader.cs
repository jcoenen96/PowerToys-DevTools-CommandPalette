using DevTools.Domain.Git;

namespace DevTools.Infrastructure.Git;

/// <summary>
/// Reads what a clone or worktree has checked out straight from its git folder, without starting git: a scan over
/// dozens of repositories stays instant.
/// </summary>
internal static class GitHeadReader
{
    /// <summary>The result of reading a working folder's <c>.git</c>.</summary>
    public sealed record GitFolder(string GitDirectory, GitHead Head, RepositoryKind Kind, string? MainRepositoryPath)
    {
        /// <summary>The remotes from the git config; a worktree shares them with its main repository.</summary>
        public IReadOnlyList<GitRemote> Remotes { get; init; } = [];
    }

    /// <returns>The git folder of <paramref name="workingDirectory"/>, or <see langword="null"/> when it is not a repository.</returns>
    public static GitFolder? Read(string workingDirectory)
    {
        var dotGit = Path.Combine(workingDirectory, ".git");
        string gitDirectory;

        if (Directory.Exists(dotGit))
        {
            gitDirectory = dotGit;
        }
        else if (File.Exists(dotGit))
        {
            // A linked worktree or a submodule: ".git" is a file with "gitdir: <path>".
            var pointer = ReadFirstLine(dotGit);
            if (pointer is null || !pointer.StartsWith("gitdir:", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            gitDirectory = Path.GetFullPath(pointer["gitdir:".Length..].Trim(), workingDirectory);
        }
        else
        {
            return null;
        }

        var head = ReadHead(gitDirectory);

        // Only linked worktrees have a "commondir" file; it points to the main repository's git folder.
        var commonDirFile = Path.Combine(gitDirectory, "commondir");
        if (File.Exists(commonDirFile) && ReadFirstLine(commonDirFile) is { Length: > 0 } commonDir)
        {
            var common = Path.GetFullPath(commonDir.Trim(), gitDirectory);
            var main = string.Equals(Path.GetFileName(common), ".git", StringComparison.OrdinalIgnoreCase)
                ? Path.GetDirectoryName(common)
                : null;
            return new GitFolder(gitDirectory, head, RepositoryKind.Worktree, main)
            {
                Remotes = GitConfigReader.ReadRemotes(common),
            };
        }

        return new GitFolder(gitDirectory, head, RepositoryKind.Clone, null)
        {
            Remotes = GitConfigReader.ReadRemotes(gitDirectory),
        };
    }

    internal static GitHead ReadHead(string gitDirectory)
    {
        var content = ReadFirstLine(Path.Combine(gitDirectory, "HEAD"));
        if (string.IsNullOrWhiteSpace(content))
        {
            return GitHead.Unknown;
        }

        content = content.Trim();
        const string prefix = "ref: ";
        if (content.StartsWith(prefix, StringComparison.Ordinal))
        {
            var reference = content[prefix.Length..].Trim();
            return new GitHead(reference.Replace("refs/heads/", "", StringComparison.Ordinal), null);
        }

        return new GitHead(null, content);
    }

    private static string? ReadFirstLine(string path)
    {
        try
        {
            using var reader = new StreamReader(path);
            return reader.ReadLine();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
