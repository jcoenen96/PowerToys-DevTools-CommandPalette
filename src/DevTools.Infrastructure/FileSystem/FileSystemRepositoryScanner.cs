using System.Collections.Concurrent;
using DevTools.Application.Abstractions;
using DevTools.Domain.Git;
using DevTools.Infrastructure.Git;

namespace DevTools.Infrastructure.FileSystem;

/// <summary>
/// Walks each root folder up to the configured depth and reports every folder with a <c>.git</c> (a clone) or a
/// <c>.git</c> file (a worktree). It does not look inside a repository it found, nor into excluded folders.
/// </summary>
public sealed class FileSystemRepositoryScanner : IRepositoryScanner
{
    public IReadOnlyList<GitRepository> Scan(
        IReadOnlyList<string> roots,
        int maxDepth,
        IReadOnlyCollection<string>? excludedFolders = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(roots);
        var found = new ConcurrentBag<GitRepository>();
        var exclusions = new Exclusions(excludedFolders ?? []);

        Parallel.ForEach(
            roots.Where(Directory.Exists),
            new ParallelOptions { CancellationToken = cancellationToken },
            root => ScanFolder(Path.GetFullPath(root), 0, Math.Max(0, maxDepth), exclusions, found, cancellationToken)
        );

        return [.. found];
    }

    private static void ScanFolder(
        string folder,
        int depth,
        int maxDepth,
        Exclusions exclusions,
        ConcurrentBag<GitRepository> found,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (GitHeadReader.Read(folder) is { } git)
        {
            var name = git.MainRepositoryPath is { } main ? Path.GetFileName(main) : Path.GetFileName(folder);
            found.Add(
                new GitRepository(name, folder, git.Head, git.Kind)
                {
                    MainRepositoryPath = git.MainRepositoryPath,
                    Remotes = git.Remotes,
                }
            );
            return;
        }

        if (depth >= maxDepth)
        {
            return;
        }

        IEnumerable<string> children;
        try
        {
            children = Directory.EnumerateDirectories(folder, "*", IgnoredFolders.Options);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var child in children)
        {
            if (!IgnoredFolders.Contains(Path.GetFileName(child)) && !exclusions.Contains(child))
            {
                ScanFolder(child, depth + 1, maxDepth, exclusions, found, cancellationToken);
            }
        }
    }

    /// <summary>Excluded folders: full paths match one folder, bare names match every folder with that name.</summary>
    private sealed class Exclusions
    {
        private static readonly char[] Separators = ['/', '\\'];
        private readonly HashSet<string> _paths = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase);

        public Exclusions(IEnumerable<string> folders)
        {
            foreach (var folder in folders.Where(f => !string.IsNullOrWhiteSpace(f)).Select(f => f.Trim()))
            {
                if (Path.IsPathRooted(folder))
                {
                    _paths.Add(Normalize(folder));
                }
                else if (folder.IndexOfAny(Separators) < 0)
                {
                    _names.Add(folder);
                }
            }
        }

        public bool Contains(string folder) =>
            _names.Contains(Path.GetFileName(folder)) || (_paths.Count > 0 && _paths.Contains(Normalize(folder)));

        private static string Normalize(string path) =>
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Replace('\\', Path.DirectorySeparatorChar)));
    }
}
