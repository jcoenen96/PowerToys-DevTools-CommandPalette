using DevTools.Application.Abstractions;
using DevTools.Domain.Settings;
using DevTools.Domain.Tools;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DevTools.CommandPalette.Settings;

/// <summary>
/// The settings page in Command Palette (Settings > Extensions > Dev tools), saved to settings.json in the
/// extension's settings folder. Folder lists are one folder per line.
/// </summary>
internal sealed partial class SettingsManager : JsonSettingsManager, ISettingsProvider
{
    private const string Namespace = "devtools";

    private readonly TextSetting _repositoryRoots = new(
        Key("repositoryRoots"),
        "Repository folders",
        "Where your repositories are cloned, one folder per line. Environment variables like %USERPROFILE% work.",
        "%USERPROFILE%\\source\\repos"
    )
    {
        Multiline = true,
    };

    private readonly TextSetting _worktreeRoots = new(
        Key("worktreeRoots"),
        "Worktree folders",
        "Where your worktrees live, one folder per line. New worktrees are created in the first one.",
        "%USERPROFILE%\\source\\worktrees"
    )
    {
        Multiline = true,
    };

    private readonly TextSetting _worktreePathTemplate = new(
        Key("worktreePathTemplate"),
        "New worktree path",
        "Where a new worktree goes. Placeholders: {worktreeRoot}, {repo}, {branch} (slashes become dashes).",
        "{worktreeRoot}\\{repo}\\{branch}"
    );

    private readonly TextSetting _scanDepth = new(
        Key("scanDepth"),
        "Repository search depth",
        "How many folder levels below each folder are searched for repositories (1 to 6).",
        "3"
    );

    private readonly TextSetting _fileSearchDepth = new(
        Key("fileSearchDepth"),
        "Solution search depth",
        "How many folder levels inside a repository are searched for solutions and other files (0 to 6).",
        "3"
    );

    private readonly TextSetting _gitExecutable = new(
        Key("gitExecutable"),
        "Git executable",
        "The git to run; a bare name is looked up on PATH.",
        "git"
    );

    private readonly Dictionary<string, ToggleSetting> _toolToggles = new(StringComparer.OrdinalIgnoreCase);
    private DevToolsSettings? _current;

    public SettingsManager(string filePath, IEnumerable<ToolDefinition> tools)
    {
        FilePath = filePath;

        Settings.Add(_repositoryRoots);
        Settings.Add(_worktreeRoots);
        Settings.Add(_worktreePathTemplate);
        Settings.Add(_scanDepth);
        Settings.Add(_fileSearchDepth);
        Settings.Add(_gitExecutable);

        foreach (var tool in tools)
        {
            var toggle = new ToggleSetting(
                Key($"tool.{tool.Id}.enabled"),
                tool.DisplayName,
                $"Offer {tool.DisplayName} (tool id \"{tool.Id}\").",
                true
            );
            _toolToggles[tool.Id] = toggle;
            Settings.Add(toggle);
        }

        LoadSettings();

        Settings.SettingsChanged += (_, _) =>
        {
            SaveSettings();
            _current = null;
            Changed?.Invoke(this, EventArgs.Empty);
        };
    }

    public event EventHandler? Changed;

    public DevToolsSettings Current => _current ??= Build();

    private DevToolsSettings Build() =>
        new()
        {
            RepositoryRoots = Lines(_repositoryRoots.Value),
            WorktreeRoots = Lines(_worktreeRoots.Value),
            WorktreePathTemplate = string.IsNullOrWhiteSpace(_worktreePathTemplate.Value)
                ? DevToolsSettings.DefaultWorktreePathTemplate
                : _worktreePathTemplate.Value.Trim(),
            ScanDepth = Number(_scanDepth.Value, fallback: 3, min: 1, max: 6),
            FileSearchDepth = Number(_fileSearchDepth.Value, fallback: 3, min: 0, max: 6),
            GitExecutable = string.IsNullOrWhiteSpace(_gitExecutable.Value) ? "git" : _gitExecutable.Value.Trim(),
            DisabledToolIds = _toolToggles
                .Where(pair => !pair.Value.Value)
                .Select(pair => pair.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
        };

    private static string Key(string name) => $"{Namespace}.{name}";

    private static string[] Lines(string? value) =>
        (value ?? "")
            .Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Trim('"'))
            .ToArray();

    private static int Number(string? value, int fallback, int min, int max) =>
        int.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var number)
            ? Math.Clamp(number, min, max)
            : fallback;
}
