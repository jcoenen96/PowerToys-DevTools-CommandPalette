using System.Text.Json.Serialization;

namespace DevTools.Infrastructure.Repositories;

/// <summary>The shape of <c>repositories.json</c>, which the user edits.</summary>
internal sealed class RepositoriesFile
{
    public List<TagEntry>? Tags { get; set; }

    public List<ActionEntry>? Actions { get; set; }

    public List<SearchEntry>? Searches { get; set; }
}

internal sealed class TagEntry
{
    public string? Tag { get; set; }

    public ConditionEntry? When { get; set; }
}

internal sealed class ActionEntry
{
    public string? Name { get; set; }

    public string? Url { get; set; }

    public string? Command { get; set; }

    public string? Arguments { get; set; }

    public string? WorkingDirectory { get; set; }

    public string? Copy { get; set; }

    public string? Icon { get; set; }

    public string? ForEachFile { get; set; }

    public ConditionEntry? When { get; set; }
}

internal sealed class ConditionEntry
{
    public string? Name { get; set; }

    public string? Path { get; set; }

    public string? Remote { get; set; }

    public string? Branch { get; set; }

    public string? FileExists { get; set; }

    public string? Tag { get; set; }

    public bool? IsWorktree { get; set; }
}

internal sealed class SearchEntry
{
    public string? Name { get; set; }

    public string? Query { get; set; }
}

/// <summary>The shape of <c>state.json</c>, which the extension writes: pins, ignored repositories, usage, searches.</summary>
internal sealed class StateFile
{
    public List<string>? Pinned { get; set; }

    public List<string>? Ignored { get; set; }

    public Dictionary<string, List<DateTimeOffset>>? Usage { get; set; }

    public List<SearchEntry>? SavedSearches { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNameCaseInsensitive = true
)]
[JsonSerializable(typeof(RepositoriesFile))]
[JsonSerializable(typeof(StateFile))]
internal sealed partial class RepositoryFilesJsonContext : JsonSerializerContext;
