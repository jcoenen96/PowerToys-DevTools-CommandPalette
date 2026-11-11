using DevTools.Domain.Git;

namespace DevTools.Application.Abstractions;

/// <summary>Finds clones and worktrees below a set of folders.</summary>
public interface IRepositoryScanner
{
    IReadOnlyList<GitRepository> Scan(
        IReadOnlyList<string> roots,
        int maxDepth,
        CancellationToken cancellationToken = default
    );
}
