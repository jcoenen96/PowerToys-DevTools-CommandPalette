using DevTools.Application.Abstractions;
using DevTools.Application.Repositories;
using DevTools.Domain.Git;

namespace DevTools.Application.Git;

/// <summary>The git use cases: branches, worktrees, and the plain commands in <see cref="GitCommands"/>.</summary>
public sealed class GitService(IGitRunner git, ISettingsProvider settings, RepositoryService repositories)
{
    public async Task<GitResult> RunAsync(
        GitRepository repository,
        GitCommand command,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(command);
        return await RunAndRefreshAsync(repository.Path, command.Arguments, cancellationToken).ConfigureAwait(false);
    }

    public async Task<GitStatus?> GetStatusAsync(
        GitRepository repository,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(repository);
        var result = await git.RunAsync(repository.Path, ["status", "--porcelain=v2", "--branch"], cancellationToken)
            .ConfigureAwait(false);
        return result.Succeeded ? GitStatus.Parse(result.Output) : null;
    }

    /// <summary>Local branches first (current one on top), then remote-tracking branches without a local twin.</summary>
    public async Task<IReadOnlyList<GitBranch>> ListBranchesAsync(
        GitRepository repository,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(repository);
        var result = await git.RunAsync(
                repository.Path,
                ["for-each-ref", $"--format={GitOutputParser.BranchFormat}", "refs/heads", "refs/remotes"],
                cancellationToken
            )
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return [];
        }

        var branches = GitOutputParser.ParseBranches(result.Output);
        var local = branches.Where(b => !b.IsRemote).Select(b => b.Name).ToHashSet(StringComparer.Ordinal);
        return
        [
            .. branches
                .Where(b => !b.IsRemote || !local.Contains(b.LocalName))
                .OrderByDescending(b => b.IsCurrent)
                .ThenBy(b => b.IsRemote)
                .ThenBy(b => b.Name, StringComparer.OrdinalIgnoreCase),
        ];
    }

    public async Task<IReadOnlyList<GitWorktree>> ListWorktreesAsync(
        GitRepository repository,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(repository);
        var result = await git.RunAsync(repository.Path, ["worktree", "list", "--porcelain"], cancellationToken)
            .ConfigureAwait(false);
        return result.Succeeded ? GitOutputParser.ParseWorktrees(result.Output) : [];
    }

    /// <summary>Checks out a branch; a remote-tracking branch gets a local branch that tracks it.</summary>
    public Task<GitResult> CheckoutAsync(
        GitRepository repository,
        GitBranch branch,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(branch);
        IReadOnlyList<string> arguments = branch.IsRemote
            ? ["switch", "--track", branch.Name]
            : ["switch", branch.Name];
        return RunAndRefreshAsync(repository.Path, arguments, cancellationToken);
    }

    public Task<GitResult> CreateBranchAsync(
        GitRepository repository,
        string name,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (!BranchNames.IsValid(name))
        {
            return Task.FromResult(new GitResult(1, "", $"'{name}' is not a valid branch name."));
        }

        return RunAndRefreshAsync(repository.Path, ["switch", "-c", name], cancellationToken);
    }

    /// <summary>
    /// Adds a worktree for an existing branch in the configured worktree folder. A remote-tracking branch gets a
    /// local branch that tracks it.
    /// </summary>
    public Task<WorktreeResult> AddWorktreeAsync(
        GitRepository repository,
        GitBranch branch,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(branch);
        return AddWorktreeAsync(
            repository,
            branch.LocalName,
            path =>
                branch.IsRemote
                    ? ["worktree", "add", "--track", "-b", branch.LocalName, path, branch.Name]
                    : ["worktree", "add", path, branch.Name],
            cancellationToken
        );
    }

    /// <summary>Creates a new branch from the current HEAD in a new worktree.</summary>
    public Task<WorktreeResult> AddWorktreeForNewBranchAsync(
        GitRepository repository,
        string name,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (!BranchNames.IsValid(name))
        {
            return Task.FromResult(WorktreeResult.Failure($"'{name}' is not a valid branch name."));
        }

        return AddWorktreeAsync(repository, name, path => ["worktree", "add", "-b", name, path], cancellationToken);
    }

    public Task<GitResult> RemoveWorktreeAsync(
        GitRepository repository,
        GitWorktree worktree,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(worktree);
        if (worktree.IsMain)
        {
            return Task.FromResult(new GitResult(1, "", "The main worktree can't be removed."));
        }

        return RunAndRefreshAsync(repository.Path, ["worktree", "remove", worktree.Path], cancellationToken);
    }

    private async Task<WorktreeResult> AddWorktreeAsync(
        GitRepository repository,
        string branch,
        Func<string, IReadOnlyList<string>> arguments,
        CancellationToken cancellationToken
    )
    {
        var path = WorktreePathBuilder.Build(settings.Current, repository, branch);
        if (path is null)
        {
            return WorktreeResult.Failure("Set a worktree folder in the Dev tools settings first.");
        }

        if (Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any())
        {
            return WorktreeResult.Failure($"{path} already exists and is not empty.");
        }

        var result = await RunAndRefreshAsync(repository.Path, arguments(path), cancellationToken)
            .ConfigureAwait(false);
        return result.Succeeded ? WorktreeResult.Success(path) : WorktreeResult.Failure(result.Summary);
    }

    private async Task<GitResult> RunAndRefreshAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken
    )
    {
        var result = await git.RunAsync(workingDirectory, arguments, cancellationToken).ConfigureAwait(false);
        repositories.Invalidate();
        return result;
    }
}

public sealed record WorktreeResult(bool Succeeded, string? Path, string Message)
{
    public static WorktreeResult Success(string path) => new(true, path, $"Worktree created in {path}");

    public static WorktreeResult Failure(string message) => new(false, null, message);
}
