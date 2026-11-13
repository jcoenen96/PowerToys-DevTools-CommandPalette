using DevTools.Application.Abstractions;
using DevTools.Domain.Repositories;

namespace DevTools.Application.Repositories;

/// <summary>
/// Pinned and ignored repositories, how often each is used, and saved searches. Unlike RepoM, pins survive a
/// restart: they are saved with the rest of the state.
/// </summary>
public sealed class RepositoryPreferences
{
    /// <summary>Usage older than this no longer counts for "most used".</summary>
    public static readonly TimeSpan UsageWindow = TimeSpan.FromDays(30);

    private const int MaxUsesPerRepository = 200;

    private readonly IRepositoryStateStore _store;
    private readonly TimeProvider _time;
    private readonly Lock _lock = new();
    private RepositoryState? _state;

    public RepositoryPreferences(IRepositoryStateStore store, TimeProvider time)
    {
        _store = store;
        _time = time;
    }

    /// <summary>Raised after a pin, ignore, use or saved search changed.</summary>
    public event EventHandler? Changed;

    public RepositoryState State
    {
        get
        {
            lock (_lock)
            {
                return _state ??= _store.Load();
            }
        }
    }

    public bool IsPinned(string path) => Contains(State.Pinned, path);

    public bool IsIgnored(string path) => Contains(State.Ignored, path);

    public IReadOnlyList<string> IgnoredPaths => State.Ignored;

    public IReadOnlyList<SavedSearch> SavedSearches => State.SavedSearches;

    public void SetPinned(string path, bool pinned) =>
        Update(state => state with { Pinned = Toggle(state.Pinned, path, pinned) });

    /// <returns>Whether the repository is pinned now.</returns>
    public bool TogglePin(string path)
    {
        var pinned = !IsPinned(path);
        SetPinned(path, pinned);
        return pinned;
    }

    public void SetIgnored(string path, bool ignored) =>
        Update(state =>
            state with
            {
                Ignored = Toggle(state.Ignored, path, ignored),
                // An ignored repository is not pinned anymore; un-ignoring doesn't bring the pin back.
                Pinned = ignored ? Toggle(state.Pinned, path, false) : state.Pinned,
            }
        );

    public void ClearIgnored() => Update(state => state with { Ignored = [] });

    /// <summary>Remembers that the repository was just used: opened in a tool, or a git command run in it.</summary>
    public void RecordUse(string path)
    {
        var now = _time.GetUtcNow();
        var key = Normalize(path);
        Update(state =>
        {
            var usage = new Dictionary<string, IReadOnlyList<DateTimeOffset>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (repository, uses) in state.Usage)
            {
                var recent = uses.Where(use => now - use < UsageWindow).ToArray();
                if (recent.Length > 0)
                {
                    usage[repository] = recent;
                }
            }

            var existing = usage.TryGetValue(key, out var list) ? list : [];
            usage[key] = [.. existing.TakeLast(MaxUsesPerRepository - 1), now];
            return state with { Usage = usage };
        });
    }

    public RepositoryUsage GetUsage(string path)
    {
        if (!State.Usage.TryGetValue(Normalize(path), out var uses) || uses.Count == 0)
        {
            return RepositoryUsage.None;
        }

        var now = _time.GetUtcNow();
        return new RepositoryUsage(uses.Count(use => now - use < UsageWindow), uses[^1]);
    }

    /// <summary>Saves a search under a name; saving the same query again only renames it.</summary>
    public void SaveSearch(string name, string query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        var search = new SavedSearch(string.IsNullOrWhiteSpace(name) ? query.Trim() : name.Trim(), query.Trim());
        Update(state =>
            state with
            {
                SavedSearches =
                [
                    .. state.SavedSearches.Where(s => !string.Equals(s.Query, search.Query, StringComparison.Ordinal)),
                    search,
                ],
            }
        );
    }

    public void DeleteSearch(SavedSearch search) =>
        Update(state => state with { SavedSearches = [.. state.SavedSearches.Where(s => s != search)] });

    /// <summary>The key a path is stored under: the full path without a trailing separator.</summary>
    public static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    private static bool Contains(IReadOnlyList<string> paths, string path)
    {
        var key = Normalize(path);
        return paths.Any(p => string.Equals(p, key, StringComparison.OrdinalIgnoreCase));
    }

    private static string[] Toggle(IReadOnlyList<string> paths, string path, bool include)
    {
        var key = Normalize(path);
        var rest = paths.Where(p => !string.Equals(p, key, StringComparison.OrdinalIgnoreCase));
        return include ? [.. rest, key] : [.. rest];
    }

    private void Update(Func<RepositoryState, RepositoryState> change)
    {
        lock (_lock)
        {
            _state = change(_state ?? _store.Load());
            _store.Save(_state);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
