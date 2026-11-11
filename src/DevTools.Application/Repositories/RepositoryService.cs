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
    private readonly Lock _lock = new();
    private IReadOnlyList<GitRepository>? _cached;
    private DateTimeOffset _cachedAt;

    public RepositoryService(IRepositoryScanner scanner, ISettingsProvider settings, TimeProvider time)
    {
        _scanner = scanner;
        _settings = settings;
        _time = time;
        _settings.Changed += (_, _) => Invalidate();
    }

    public IReadOnlyList<GitRepository> GetRepositories(bool refresh = false)
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
    }
}
