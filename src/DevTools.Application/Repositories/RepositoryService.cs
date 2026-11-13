using DevTools.Application.Abstractions;
using DevTools.Domain.Git;

namespace DevTools.Application.Repositories;

/// <summary>
/// The clones and worktrees under the configured repository and worktree folders. Scanning touches the disk, so the
/// result is kept for a short while; opening a page again within that time is instant.
/// </summary>
public sealed class RepositoryService
{
    public static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    private readonly IRepositoryScanner _scanner;
    private readonly ISettingsProvider _settings;
    private readonly TimeProvider _time;
    private readonly RepositoryPreferences? _preferences;
    private readonly Lock _lock = new();
    private IReadOnlyList<GitRepository>? _cached;
    private DateTimeOffset _cachedAt;

    /// <param name="scanner">Finds the repositories on disk.</param>
    /// <param name="settings">The folders to search.</param>
    /// <param name="time">The clock the cache expires by.</param>
    /// <param name="preferences">When given, ignored repositories are left out.</param>
    public RepositoryService(
        IRepositoryScanner scanner,
        ISettingsProvider settings,
        TimeProvider time,
        RepositoryPreferences? preferences = null
    )
    {
        _scanner = scanner;
        _settings = settings;
        _time = time;
        _preferences = preferences;
        _settings.Changed += (_, _) => Invalidate();
    }

    /// <summary>Raised when the cached scan is thrown away, e.g. after a checkout; lists should reload.</summary>
    public event EventHandler? Invalidated;

    /// <summary>The clones and worktrees, without the ignored ones.</summary>
    public IReadOnlyList<GitRepository> GetRepositories(bool refresh = false)
    {
        var all = GetAllRepositories(refresh);
        return _preferences is { } preferences && preferences.IgnoredPaths.Count > 0
            ? [.. all.Where(repository => !preferences.IsIgnored(repository.Path))]
            : all;
    }

    /// <summary>The clones and worktrees, the ignored ones included.</summary>
    public IReadOnlyList<GitRepository> GetAllRepositories(bool refresh = false)
    {
        lock (_lock)
        {
            if (!refresh && _cached is not null && _time.GetUtcNow() - _cachedAt < CacheDuration)
            {
                return _cached;
            }
        }

        var settings = _settings.Current;
        var roots = settings
            .RepositoryRoots.Concat(settings.WorktreeRoots)
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(root => Environment.ExpandEnvironmentVariables(root.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var repositories = _scanner
            .Scan(
                roots,
                settings.ScanDepth,
                [
                    .. settings
                        .ExcludedFolders.Where(folder => !string.IsNullOrWhiteSpace(folder))
                        .Select(folder => Environment.ExpandEnvironmentVariables(folder.Trim())),
                ]
            )
            .DistinctBy(repository => Path.GetFullPath(repository.Path), StringComparer.OrdinalIgnoreCase)
            .OrderBy(repository => repository.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(repository => repository.Kind)
            .ThenBy(repository => repository.Head.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        lock (_lock)
        {
            _cached = repositories;
            _cachedAt = _time.GetUtcNow();
        }

        return repositories;
    }

    /// <summary>Forgets the cached scan, e.g. after a checkout or a new worktree.</summary>
    public void Invalidate()
    {
        lock (_lock)
        {
            _cached = null;
        }

        Invalidated?.Invoke(this, EventArgs.Empty);
    }
}
