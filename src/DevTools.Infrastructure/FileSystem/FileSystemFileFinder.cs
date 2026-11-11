using DevTools.Application.Abstractions;

namespace DevTools.Infrastructure.FileSystem;

public sealed class FileSystemFileFinder : IFileFinder
{
    public IReadOnlyList<string> Find(string directory, IReadOnlyList<string> patterns, int maxDepth)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        var results = new List<string>();
        if (Directory.Exists(directory) && patterns.Count > 0)
        {
            Search(directory, patterns, 0, Math.Max(0, maxDepth), results);
        }

        return [.. results.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static void Search(
        string folder,
        IReadOnlyList<string> patterns,
        int depth,
        int maxDepth,
        List<string> results
    )
    {
        try
        {
            foreach (var pattern in patterns)
            {
                results.AddRange(Directory.EnumerateFiles(folder, pattern, IgnoredFolders.Options));
            }

            if (depth >= maxDepth)
            {
                return;
            }

            foreach (var child in Directory.EnumerateDirectories(folder, "*", IgnoredFolders.Options))
            {
                var name = Path.GetFileName(child);
                // A nested ".git" means a submodule or a worktree inside this repository; it is listed on its own.
                if (!IgnoredFolders.Contains(name) && !Path.Exists(Path.Combine(child, ".git")))
                {
                    Search(child, patterns, depth + 1, maxDepth, results);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A folder we may not read; skip it.
        }
    }
}
