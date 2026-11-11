namespace DevTools.Domain.Tools;

/// <summary>
/// A tool the extension can launch, such as Visual Studio or Rider. Presets ship with the extension;
/// users add or override tools in <c>tools.json</c>.
/// </summary>
public sealed record ToolDefinition
{
    /// <summary>Stable id; Command Palette keys hotkeys and aliases on it, so it never changes for a preset.</summary>
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>
    /// Where the executable may be, tried in order. Each entry is a path (environment variables and
    /// <c>*</c> wildcards allowed), a bare name looked up on PATH, or <c>vswhere:&lt;args&gt;</c>.
    /// </summary>
    public IReadOnlyList<string> Executables { get; init; } = [];

    /// <summary>The command line passed to the tool; see <see cref="ArgumentPlaceholders"/>.</summary>
    public string Arguments { get; init; } = $"\"{ArgumentPlaceholders.Target}\"";

    public ToolTargetKind TargetKind { get; init; } = ToolTargetKind.Folder;

    /// <summary>File name patterns for <see cref="ToolTargetKind.Files"/>, e.g. <c>*.slnx</c>.</summary>
    public IReadOnlyList<string> FilePatterns { get; init; } = [];

    /// <summary>When a repository has no matching files, offer the folder instead.</summary>
    public bool FallBackToFolder { get; init; }

    /// <summary>The icon used when none is set: the palette then shows the icon of the tool's executable.</summary>
    public const string DefaultIcon = "\uE943";

    /// <summary>A Segoe Fluent icon glyph, emoji, or path to an image or executable.</summary>
    public string Icon { get; init; } = DefaultIcon;

    /// <summary>Whether the tool shows up as its own top-level command (and so can get its own hotkey).</summary>
    public bool ShowAsTopLevelCommand { get; init; } = true;

    public bool IsPreset { get; init; }
}
