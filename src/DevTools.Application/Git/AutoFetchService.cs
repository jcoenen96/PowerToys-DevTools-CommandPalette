using DevTools.Application.Abstractions;
using DevTools.Application.Repositories;
using DevTools.Domain.Git;

namespace DevTools.Application.Git;

/// <summary>
/// Fetches every repository in the background at the interval from the settings (off by default), so ahead and
/// behind counts stay current, like RepoM's auto fetch. A clone and its worktrees share their remotes, so each
/// project is fetched once.
/// </summary>
public sealed class AutoFetchService : IDisposable
{
    private readonly GitService _git;
    private readonly RepositoryService _repositories;
    private readonly ISettingsProvider _settings;
    private readonly TimeProvider _time;
    private readonly Lock _lock = new();
    private readonly CancellationTokenSource _stop = new();
    private ITimer? _timer;
    private TimeSpan _interval;
    private int _running;

    public AutoFetchService(
        GitService git,
        RepositoryService repositories,
        ISettingsProvider settings,
        TimeProvider time
    )
    {
        _git = git;
        _repositories = repositories;
        _settings = settings;
        _time = time;
        _settings.Changed += (_, _) => Schedule();
        Schedule();
    }

    /// <summary>Raised after a round of fetches, with how many repositories were fetched and how many failed.</summary>
    public event EventHandler<FetchRound>? Fetched;

    /// <summary>The interval the timer runs at now; <see cref="TimeSpan.Zero"/> when auto fetch is off.</summary>
    public TimeSpan Interval
    {
        get
        {
            lock (_lock)
            {
                return _interval;
            }
        }
    }

    /// <summary>Fetches every project once; a round that is still running is not started again.</summary>
    public async Task<FetchRound> FetchAllAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _running, 1) == 1)
        {
            return new FetchRound(0, 0, AlreadyRunning: true);
        }

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stop.Token);
            var prune = _settings.Current.PruneOnFetch;
            int fetched = 0,
                failed = 0;
            foreach (var repository in OnePerProject(_repositories.GetRepositories()))
            {
                linked.Token.ThrowIfCancellationRequested();
                var result = await _git.FetchAsync(repository, prune, linked.Token).ConfigureAwait(false);
                if (result.Succeeded)
                {
                    fetched++;
                }
                else
                {
                    failed++;
                }
            }

            var round = new FetchRound(fetched, failed, AlreadyRunning: false);
            Fetched?.Invoke(this, round);
            return round;
        }
        finally
        {
            Volatile.Write(ref _running, 0);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
        }

        _stop.Cancel();
        _stop.Dispose();
    }

    /// <summary>The clone of each project (or its first worktree when the clone isn't in the folders), with a remote.</summary>
    internal static IEnumerable<GitRepository> OnePerProject(IEnumerable<GitRepository> repositories) =>
        repositories
            .Where(repository => repository.Remotes.Count > 0)
            .GroupBy(
                repository =>
                    Path.TrimEndingDirectorySeparator(
                        repository.IsWorktree ? repository.MainRepositoryPath ?? repository.Path : repository.Path
                    ),
                StringComparer.OrdinalIgnoreCase
            )
            .Select(group => group.OrderBy(repository => repository.IsWorktree).First());

    private void Schedule()
    {
        var interval = _settings.Current.AutoFetchInterval;
        lock (_lock)
        {
            if (interval == _interval && (_timer is not null || interval <= TimeSpan.Zero))
            {
                return;
            }

            _interval = interval;
            _timer?.Dispose();
            _timer =
                interval > TimeSpan.Zero ? _time.CreateTimer(_ => _ = TickAsync(), null, interval, interval) : null;
        }
    }

    private async Task TickAsync()
    {
        try
        {
            await FetchAllAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Stopped while fetching.
        }
#pragma warning disable CA1031 // A background fetch must never take the extension down; the next round tries again.
        catch (Exception)
#pragma warning restore CA1031
        {
            // Network or git trouble; nothing to show in the background.
        }
    }
}

public sealed record FetchRound(int Fetched, int Failed, bool AlreadyRunning);
