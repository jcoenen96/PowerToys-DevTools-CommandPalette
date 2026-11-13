using System.Collections.Concurrent;
using DevTools.Domain.Git;

namespace DevTools.Application.Git;

/// <summary>
/// The <c>git status</c> of each repository, for the ahead, behind and changed counts in the lists. Lists show
/// what is known right away and ask for the rest with <see cref="RefreshAsync"/>, which runs a few statuses at a
/// time and raises <see cref="Updated"/> when done. A status is kept for a minute, and dropped as soon as a command
/// runs in that repository.
/// </summary>
public sealed class GitStatusCache
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(1);

    private const int Parallelism = 4;

    private readonly GitService _git;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<string, (GitStatus? Status, DateTimeOffset At)> _statuses = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly ConcurrentDictionary<string, byte> _running = new(StringComparer.OrdinalIgnoreCase);

    public GitStatusCache(GitService git, TimeProvider time)
    {
        _git = git;
        _time = time;
        _git.RepositoryChanged += (_, path) => Forget(path);
    }

    /// <summary>Raised after <see cref="RefreshAsync"/> learned new statuses.</summary>
    public event EventHandler? Updated;

    /// <summary>The last known status, or <see langword="null"/> when there is none (or git failed).</summary>
    public GitStatus? Get(GitRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return _statuses.TryGetValue(Key(repository.Path), out var known) ? known.Status : null;
    }

    /// <summary>Whether there is no status yet, or it is older than <see cref="Lifetime"/>.</summary>
    public bool IsStale(GitRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return !_statuses.TryGetValue(Key(repository.Path), out var known) || _time.GetUtcNow() - known.At >= Lifetime;
    }

    /// <summary>Reads the status of every stale repository; the ones being read already are skipped.</summary>
    public async Task RefreshAsync(
        IEnumerable<GitRepository> repositories,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(repositories);
        var stale = repositories.Where(IsStale).Where(r => _running.TryAdd(Key(r.Path), 0)).ToList();
        if (stale.Count == 0)
        {
            return;
        }

        try
        {
            await Parallel
                .ForEachAsync(
                    stale,
                    new ParallelOptions { MaxDegreeOfParallelism = Parallelism, CancellationToken = cancellationToken },
                    async (repository, token) =>
                    {
                        var status = await _git.GetStatusAsync(repository, token).ConfigureAwait(false);
                        _statuses[Key(repository.Path)] = (status, _time.GetUtcNow());
                    }
                )
                .ConfigureAwait(false);
        }
        finally
        {
            foreach (var repository in stale)
            {
                _running.TryRemove(Key(repository.Path), out _);
            }
        }

        Updated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Drops the status of one repository, e.g. after a pull.</summary>
    public void Forget(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _statuses.TryRemove(Key(path), out _);
    }

    private static string Key(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
