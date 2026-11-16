using DevTools.Domain.Git;

namespace DevTools.Infrastructure.Git;

/// <summary>Reads the remotes straight from a repository's <c>config</c> file, without starting git.</summary>
internal static class GitConfigReader
{
    /// <returns>The remotes with a URL, <c>origin</c> first and the rest by name.</returns>
    public static IReadOnlyList<GitRemote> ReadRemotes(string gitDirectory)
    {
        string[] lines;
        try
        {
            var path = Path.Combine(gitDirectory, "config");
            if (!File.Exists(path))
            {
                return [];
            }

            lines = File.ReadAllLines(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }

        return Parse(lines);
    }

    internal static IReadOnlyList<GitRemote> Parse(IEnumerable<string> lines)
    {
        var remotes = new Dictionary<string, string>(StringComparer.Ordinal);
        string? remote = null;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] is '#' or ';')
            {
                continue;
            }

            if (line[0] == '[')
            {
                remote = SectionRemote(line);
                continue;
            }

            if (remote is null)
            {
                continue;
            }

            var equals = line.IndexOf('=', StringComparison.Ordinal);
            if (equals > 0 && string.Equals(line[..equals].Trim(), "url", StringComparison.OrdinalIgnoreCase))
            {
                // The first url of a remote is the one git fetches from.
                remotes.TryAdd(remote, Unquote(line[(equals + 1)..].Trim()));
            }
        }

        return
        [
            .. remotes
                .Where(pair => pair.Value.Length > 0)
                .OrderBy(pair => pair.Key != "origin")
                .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => new GitRemote(pair.Key, pair.Value)),
        ];
    }

    /// <summary>The name in <c>[remote "origin"]</c>, or <see langword="null"/> for any other section.</summary>
    private static string? SectionRemote(string line)
    {
        var end = line.IndexOf(']', StringComparison.Ordinal);
        var header = end > 0 ? line[1..end].Trim() : line[1..];
        if (!header.StartsWith("remote", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var name = header["remote".Length..].Trim();
        return name.Length >= 2 && name[0] == '"' && name[^1] == '"' ? name[1..^1] : null;
    }

    private static string Unquote(string value)
    {
        var comment = value.IndexOfAny(['#', ';']);
        if (comment >= 0 && !value.StartsWith('"'))
        {
            value = value[..comment].Trim();
        }

        return value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;
    }
}
