using DevTools.Application.Abstractions;
using DevTools.Domain.Git;
using DevTools.Infrastructure.Git;

namespace DevTools.Infrastructure.FileSystem;

/// <summary>
/// Walks each root folder up to the configured depth and reports every folder with a <c>.git</c> (a clone) or a
/// <c>.git</c> file (a worktree). It does not look inside a repository it found.
/// </summary>
public sealed class FileSystemRepositoryScanner : IRepositoryScanner
{
    public IReadOnlyList<GitRepository> Scan(
        IReadOnlyList<string> roots,
        int maxDepth,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(roots);
        var found = new System.Collections.Concurrent.ConcurrentBag<GitRepository>();

        Parallel.ForEach(
            roots.Where(Directory.Exists),
            new ParallelOptions { CancellationToken = cancellationToken },
            root => ScanFolder(Path.GetFullPath(root), 0, Math.Max(0, maxDepth), found, cancellationToken)
        );

        return [.. found];
    }

    private static void ScanFolder(
        string folder,
        int depth,
        int maxDepth,
        System.Collections.Concurrent.ConcurrentBag<GitRepository> found,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (GitHeadReader.Read(folder) is { } git)
        {
            var name = git.MainRepositoryPath is { } main ? Path.GetFileName(main) : Path.GetFileName(folder);
            found.Add(
                new GitRepository(name, folder, git.Head, git.Kind) { MainRepositoryPath = git.MainRepositoryPath }
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
            if (!IgnoredFolders.Contains(Path.GetFileName(child)))
            {
                ScanFolder(child, depth + 1, maxDepth, found, cancellationToken);
            }
        }
    }
}
