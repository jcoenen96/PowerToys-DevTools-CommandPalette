using DevTools.Domain.Git;

namespace DevTools.Application.Abstractions;

/// <summary>Finds clones and worktrees below a set of folders.</summary>
public interface IRepositoryScanner
{
    /// <param name="roots">The folders to search; a root is always searched, even when it is also excluded.</param>
    /// <param name="maxDepth">How many folder levels below a root are searched.</param>
    /// <param name="excludedFolders">
    /// Folders not searched below a root: a full path skips exactly that folder, a bare name (such as
    /// <c>worktrees</c>) skips every folder with that name.
    /// </param>
    /// <param name="cancellationToken">Stops the scan.</param>
    IReadOnlyList<GitRepository> Scan(
        IReadOnlyList<string> roots,
        int maxDepth,
        IReadOnlyCollection<string>? excludedFolders = null,
        CancellationToken cancellationToken = default
    );
}
