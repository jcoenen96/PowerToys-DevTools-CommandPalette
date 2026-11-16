using System.Text.Json;
using DevTools.Application.Abstractions;
using DevTools.Application.Tools;
using DevTools.Domain.Tools;

namespace DevTools.Infrastructure.Tools;

/// <summary>
/// The presets merged with the user's <c>tools.json</c>: an entry with a preset's id changes only the fields it sets,
/// any other entry adds a tool. The file is watched, so edits apply without restarting Command Palette.
/// </summary>
public sealed class JsonToolCatalog : IToolCatalog, IDisposable
{
    internal const string ExampleFile = """
        {
          // Your own tools, and changes to the presets. Saved changes apply right away.
          //
          // An entry with the id of a preset only changes what it sets, e.g. a Rider installed elsewhere:
          //   { "id": "rider", "executables": ["D:\\Tools\\Rider\\bin\\rider64.exe"] }
          //
          // Any other id adds a tool, with its own "Open in ..." command (so it can get a hotkey) and its own switch
          // on the Dev tools settings page. Fields:
          //   id                     stable id; Command Palette keeps the tool's hotkey and alias under it
          //   displayName            the name in the palette
          //   executables            tried in order: a path (%VARIABLES% and * wildcards allowed, newest match wins),
          //                          a name on PATH, or "vswhere:<arguments>"
          //   arguments              default "\"{target}\""; placeholders: {target} {folder} {repo} {branch} {name}
          //   targetKind             "Folder" (open the repository folder) or "Files" (open files matching filePatterns)
          //   filePatterns           e.g. ["*.slnx", "*.sln"]; earlier patterns win when a folder has both
          //   fallBackToFolder       with "Files": offer the folder when a repository has no matching file
          //   icon                   optional; by default the icon of the program that launches the tool (also
          //                          for a name on PATH, a .cmd launcher or an app alias like wt.exe). Set a
          //                          Segoe Fluent Icons glyph, an emoji, or a path to an .exe, .ico or .png
          //
          // Preset ids: visualstudio, vscode, vscode-insiders, rider, intellij-idea, webstorm, pycharm, goland,
          // clion, rustrover, datagrip, orca, terminal, explorer.
          "tools": [
            // {
            //   "id": "sublime",
            //   "displayName": "Sublime Text",
            //   "executables": ["%ProgramFiles%\\Sublime Text\\sublime_text.exe"],
            //   "targetKind": "Folder"
            // }
          ]
        }

        """;

    private readonly IReadOnlyList<ToolDefinition> _presets;
    private readonly FileSystemWatcher? _watcher;
    private readonly Lock _lock = new();
    private IReadOnlyList<ToolDefinition>? _tools;

    public JsonToolCatalog(string configurationPath, IReadOnlyList<ToolDefinition>? presets = null, bool watch = true)
    {
        ConfigurationPath = configurationPath;
        _presets = presets ?? ToolPresets.All;

        var folder = Path.GetDirectoryName(configurationPath);
        if (watch && folder is not null && Directory.Exists(folder))
        {
            _watcher = new FileSystemWatcher(folder, Path.GetFileName(configurationPath))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };
            _watcher.Changed += (_, _) => Reload();
            _watcher.Created += (_, _) => Reload();
            _watcher.Deleted += (_, _) => Reload();
            _watcher.Renamed += (_, _) => Reload();
        }
    }

    public string ConfigurationPath { get; }

    public string? ConfigurationError { get; private set; }

    public event EventHandler? Changed;

    public IReadOnlyList<ToolDefinition> GetTools()
    {
        lock (_lock)
        {
            return _tools ??= Load();
        }
    }

    public void EnsureConfigurationFile()
    {
        if (!File.Exists(ConfigurationPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigurationPath)!);
            File.WriteAllText(ConfigurationPath, ExampleFile);
        }
    }

    public void Dispose() => _watcher?.Dispose();

    private void Reload()
    {
        lock (_lock)
        {
            _tools = null;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private List<ToolDefinition> Load()
    {
        ConfigurationError = null;
        var tools = _presets.ToList();
        if (!File.Exists(ConfigurationPath))
        {
            return tools;
        }

        ToolsFile? file;
        try
        {
            file = JsonSerializer.Deserialize(ReadShared(ConfigurationPath), ToolsFileJsonContext.Default.ToolsFile);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            ConfigurationError = $"tools.json could not be read: {exception.Message}";
            return tools;
        }

        foreach (var entry in file?.Tools ?? [])
        {
            if (string.IsNullOrWhiteSpace(entry.Id))
            {
                ConfigurationError = "tools.json has a tool without an id; it was skipped.";
                continue;
            }

            var index = tools.FindIndex(tool => string.Equals(tool.Id, entry.Id, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                tools[index] = Apply(entry, tools[index]);
            }
            else if (entry.Executables is { Count: > 0 })
            {
                tools.Add(Apply(entry, new ToolDefinition { Id = entry.Id.Trim(), DisplayName = entry.Id.Trim() }));
            }
            else
            {
                ConfigurationError = $"The tool '{entry.Id}' in tools.json has no executables; it was skipped.";
            }
        }

        return tools;
    }

    private static ToolDefinition Apply(ToolEntry entry, ToolDefinition tool) =>
        tool with
        {
            DisplayName = string.IsNullOrWhiteSpace(entry.DisplayName) ? tool.DisplayName : entry.DisplayName,
            Executables = entry.Executables is { Count: > 0 } executables ? executables : tool.Executables,
            Arguments = string.IsNullOrWhiteSpace(entry.Arguments) ? tool.Arguments : entry.Arguments,
            TargetKind = entry.TargetKind ?? tool.TargetKind,
            FilePatterns = entry.FilePatterns ?? tool.FilePatterns,
            FallBackToFolder = entry.FallBackToFolder ?? tool.FallBackToFolder,
            Icon = string.IsNullOrWhiteSpace(entry.Icon) ? tool.Icon : entry.Icon,
        };

    /// <summary>Reads the file while an editor may still hold it open.</summary>
    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
