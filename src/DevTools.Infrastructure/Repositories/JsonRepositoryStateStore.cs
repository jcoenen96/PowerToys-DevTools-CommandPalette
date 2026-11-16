using System.Text.Json;
using DevTools.Application.Abstractions;
using DevTools.Domain.Repositories;

namespace DevTools.Infrastructure.Repositories;

/// <summary>Keeps pins, ignored repositories, usage and saved searches in <c>state.json</c>.</summary>
public sealed class JsonRepositoryStateStore(string path) : IRepositoryStateStore
{
    public string Path { get; } = path;

    public RepositoryState Load()
    {
        StateFile? file;
        try
        {
            if (!File.Exists(Path))
            {
                return RepositoryState.Empty;
            }

            file = JsonSerializer.Deserialize(JsonFiles.ReadShared(Path), RepositoryFilesJsonContext.Default.StateFile);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            // A broken state file only loses pins and usage; it must not break the extension.
            return RepositoryState.Empty;
        }

        if (file is null)
        {
            return RepositoryState.Empty;
        }

        var usage = new Dictionary<string, IReadOnlyList<DateTimeOffset>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (repository, uses) in file.Usage ?? [])
        {
            usage[repository] = [.. uses.Order()];
        }

        return new RepositoryState
        {
            Pinned = file.Pinned ?? [],
            Ignored = file.Ignored ?? [],
            Usage = usage,
            SavedSearches =
            [
                .. (file.SavedSearches ?? [])
                    .Where(search => !string.IsNullOrWhiteSpace(search.Query))
                    .Select(search => new SavedSearch(search.Name ?? search.Query!, search.Query!)),
            ],
        };
    }

    public void Save(RepositoryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var file = new StateFile
        {
            Pinned = [.. state.Pinned],
            Ignored = [.. state.Ignored],
            Usage = state.Usage.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToList(),
                StringComparer.OrdinalIgnoreCase
            ),
            SavedSearches = [.. state.SavedSearches.Select(s => new SearchEntry { Name = s.Name, Query = s.Query })],
        };

        try
        {
            JsonFiles.WriteAtomically(
                Path,
                JsonSerializer.Serialize(file, RepositoryFilesJsonContext.Default.StateFile)
            );
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Keep working with the state in memory; the next save tries again.
        }
    }
}

/// <summary>A state store that forgets everything when the process ends, for tests and as a fallback.</summary>
public sealed class InMemoryRepositoryStateStore : IRepositoryStateStore
{
    private RepositoryState _state = RepositoryState.Empty;

    public RepositoryState Load() => _state;

    public void Save(RepositoryState state) => _state = state ?? throw new ArgumentNullException(nameof(state));
}

/// <summary>A configuration without tags, actions or searches.</summary>
public sealed class EmptyRepositoryConfiguration : IRepositoryConfiguration
{
    public string ConfigurationPath => "";

    public RepositoryConfiguration Current { get; set; } = RepositoryConfiguration.Empty;

    public string? ConfigurationError => null;

    public void EnsureConfigurationFile() { }

    public event EventHandler? Changed;

    /// <summary>Replaces the configuration, as an edit of the file would.</summary>
    public void Set(RepositoryConfiguration configuration)
    {
        Current = configuration;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
