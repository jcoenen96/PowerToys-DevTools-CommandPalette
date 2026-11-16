using System.Text.Json.Serialization;
using DevTools.Domain.Tools;

namespace DevTools.Infrastructure.Tools;

/// <summary>The shape of <c>tools.json</c>. Every field but the id is optional when overriding a preset.</summary>
internal sealed class ToolsFile
{
    public List<ToolEntry> Tools { get; set; } = [];
}

internal sealed class ToolEntry
{
    public string? Id { get; set; }

    public string? DisplayName { get; set; }

    public List<string>? Executables { get; set; }

    public string? Arguments { get; set; }

    public ToolTargetKind? TargetKind { get; set; }

    public List<string>? FilePatterns { get; set; }

    public bool? FallBackToFolder { get; set; }

    public string? Icon { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNameCaseInsensitive = true,
    UseStringEnumConverter = true
)]
[JsonSerializable(typeof(ToolsFile))]
internal sealed partial class ToolsFileJsonContext : JsonSerializerContext;
