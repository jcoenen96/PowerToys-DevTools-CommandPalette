using DevTools.Domain.Git;
using DevTools.Domain.Repositories;

namespace DevTools.Application.Repositories;

/// <summary>Checks a <see cref="RepositoryCondition"/> against a repository.</summary>
public static class RepositoryConditions
{
    public static bool Matches(
        RepositoryCondition condition,
        GitRepository repository,
        IReadOnlyCollection<string>? tags = null
    )
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(repository);

        if (condition.IsWorktree is { } worktree && worktree != repository.IsWorktree)
        {
            return false;
        }

        if (condition.Name is { Length: > 0 } name && !Wildcard.IsMatch(repository.Name, name))
        {
            return false;
        }

        if (condition.Branch is { Length: > 0 } branch && !Wildcard.IsMatch(repository.Head.DisplayName, branch))
        {
            return false;
        }

        if (
            condition.Path is { Length: > 0 } path
            && !Slashes(repository.Path).Contains(Slashes(path), StringComparison.OrdinalIgnoreCase)
        )
        {
            return false;
        }

        if (
            condition.Remote is { Length: > 0 } remote
            && !repository.Remotes.Any(r => r.Url.Contains(remote, StringComparison.OrdinalIgnoreCase))
        )
        {
            return false;
        }

        if (
            condition.Tag is { Length: > 0 } tag
            && (tags is null || !tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
        )
        {
            return false;
        }

        return condition.FileExists is not { Length: > 0 } file || FileExists(repository.Path, file);
    }

    private static string Slashes(string path) => path.Replace('\\', '/');

    /// <summary>A path relative to the repository; the last part may contain wildcards.</summary>
    private static bool FileExists(string root, string relativePath)
    {
        var path = Path.Combine(root, relativePath.Replace('\\', '/').TrimStart('/'));
        var folder = Path.GetDirectoryName(path);
        var pattern = Path.GetFileName(path);
        if (folder is null || pattern.Length == 0)
        {
            return Directory.Exists(path);
        }

        try
        {
            return Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder, pattern).Any();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }
}

/// <summary>Case-insensitive matching with <c>*</c> (any text) and <c>?</c> (one character).</summary>
public static class Wildcard
{
    public static bool IsMatch(string text, string pattern)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(pattern);
        return IsMatch(text.AsSpan(), pattern.AsSpan());
    }

    private static bool IsMatch(ReadOnlySpan<char> text, ReadOnlySpan<char> pattern)
    {
        int t = 0,
            p = 0,
            star = -1,
            mark = 0;
        while (t < text.Length)
        {
            if (
                p < pattern.Length
                && (pattern[p] == '?' || char.ToUpperInvariant(pattern[p]) == char.ToUpperInvariant(text[t]))
            )
            {
                t++;
                p++;
            }
            else if (p < pattern.Length && pattern[p] == '*')
            {
                star = p++;
                mark = t;
            }
            else if (star >= 0)
            {
                p = star + 1;
                t = ++mark;
            }
            else
            {
                return false;
            }
        }

        while (p < pattern.Length && pattern[p] == '*')
        {
            p++;
        }

        return p == pattern.Length;
    }
}
