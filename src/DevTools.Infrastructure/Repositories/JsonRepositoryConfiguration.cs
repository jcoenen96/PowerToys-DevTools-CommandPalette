using System.Text.Json;
using DevTools.Application.Abstractions;
using DevTools.Domain.Repositories;

namespace DevTools.Infrastructure.Repositories;

/// <summary>
/// The user's <c>repositories.json</c>: tags, custom actions and saved searches. The file is watched, so edits apply
/// without restarting Command Palette.
/// </summary>
public sealed class JsonRepositoryConfiguration : IRepositoryConfiguration, IDisposable
{
    internal const string ExampleFile = """
        {
          // Tags, custom actions and saved searches for your repositories. Saved changes apply right away.
          //
          // A "when" picks the repositories; every field you set must match (text ignores case):
          //   name        repository name, * and ? wildcards        "Shop*"
          //   path        part of its path                          "source/repos/work"
          //   remote      part of a remote URL                      "dev.azure.com/my-org"
          //   branch      checked-out branch, wildcards             "feature/*"
          //   fileExists  a file in the repository, wildcards       "*.slnx", "src/package.json"
          //   isWorktree  true for worktrees, false for clones
          //   tag         (actions only) a tag the repository has   "work"
          //
          // Tags show on every repository and can be searched: "tag:work", "-tag:archived", or just "work".
          "tags": [
            // { "tag": "work", "when": { "remote": "dev.azure.com/my-org" } },
            // { "tag": "github", "when": { "remote": "github.com" } },
            // { "tag": "dotnet", "when": { "fileExists": "*.slnx" } }
          ],

          // Actions show in the Ctrl+K menu and on the git page of every repository they apply to. Set one of:
          //   url      opens in your browser                    "{webUrl}/wiki"
          //   command  starts a program, with "arguments"       "cmd.exe" with "/k dotnet build"
          //   copy     copies text to the clipboard             "{remoteUrl}"
          // Optional: "workingDirectory" (the repository by default), "icon" (a glyph, emoji or image path), "when",
          // and "forEachFile": one action per matching file, with {file} and {fileName} set to it.
          // Placeholders: {path} {name} {branch} {remoteUrl} {webUrl} {file} {fileName}, and %VARIABLES%.
          "actions": [
            // { "name": "Wiki", "url": "{webUrl}/wiki", "when": { "remote": "github.com" } },
            // { "name": "dotnet test {fileName}", "command": "cmd.exe", "arguments": "/k dotnet test \"{file}\"", "forEachFile": "*.Tests.csproj" },
            // { "name": "Copy clone URL", "copy": "{remoteUrl}" }
          ],

          // Searches listed on the Git repositories page. Searches you save from the palette are kept separately.
          "searches": [
            // { "name": "Work with changes", "query": "tag:work has:changes" }
          ]
        }

        """;

    private readonly FileSystemWatcher? _watcher;
    private readonly Lock _lock = new();
    private RepositoryConfiguration? _current;

    public JsonRepositoryConfiguration(string configurationPath, bool watch = true)
    {
        ConfigurationPath = configurationPath;
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

    public RepositoryConfiguration Current
    {
        get
        {
            lock (_lock)
            {
                return _current ??= Load();
            }
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
            _current = null;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private RepositoryConfiguration Load()
    {
        ConfigurationError = null;
        if (!File.Exists(ConfigurationPath))
        {
            return RepositoryConfiguration.Empty;
        }

        RepositoriesFile? file;
        try
        {
            file = JsonSerializer.Deserialize(
                JsonFiles.ReadShared(ConfigurationPath),
                RepositoryFilesJsonContext.Default.RepositoriesFile
            );
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            ConfigurationError = $"repositories.json could not be read: {exception.Message}";
            return RepositoryConfiguration.Empty;
        }

        var actions = new List<RepositoryAction>();
        foreach (var entry in file?.Actions ?? [])
        {
            if (ToAction(entry) is { } action)
            {
                actions.Add(action);
            }
            else
            {
                ConfigurationError =
                    $"The action '{entry.Name}' in repositories.json needs a name and one of url, command or copy; it was skipped.";
            }
        }

        return new RepositoryConfiguration
        {
            Tags =
            [
                .. (file?.Tags ?? [])
                    .Where(entry => !string.IsNullOrWhiteSpace(entry.Tag))
                    .Select(entry => new TagRule(entry.Tag!.Trim(), ToCondition(entry.When))),
            ],
            Actions = actions,
            Searches =
            [
                .. (file?.Searches ?? [])
                    .Where(entry => !string.IsNullOrWhiteSpace(entry.Query))
                    .Select(entry => new SavedSearch(
                        string.IsNullOrWhiteSpace(entry.Name) ? entry.Query!.Trim() : entry.Name.Trim(),
                        entry.Query!.Trim()
                    )),
            ],
        };
    }

    private static RepositoryAction? ToAction(ActionEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Name))
        {
            return null;
        }

        var common = new RepositoryAction
        {
            Name = entry.Name.Trim(),
            Icon = entry.Icon,
            When = ToCondition(entry.When),
            ForEachFile = entry.ForEachFile,
            WorkingDirectory = entry.WorkingDirectory,
        };

        if (!string.IsNullOrWhiteSpace(entry.Url))
        {
            return common with { Kind = RepositoryActionKind.Url, Url = entry.Url };
        }

        if (!string.IsNullOrWhiteSpace(entry.Command))
        {
            return common with
            {
                Kind = RepositoryActionKind.Command,
                Executable = entry.Command,
                Arguments = entry.Arguments,
            };
        }

        return string.IsNullOrEmpty(entry.Copy)
            ? null
            : common with
            {
                Kind = RepositoryActionKind.Copy,
                Text = entry.Copy,
            };
    }

    private static RepositoryCondition ToCondition(ConditionEntry? entry) =>
        entry is null
            ? RepositoryCondition.Always
            : new RepositoryCondition
            {
                Name = Blank(entry.Name),
                Path = Blank(entry.Path),
                Remote = Blank(entry.Remote),
                Branch = Blank(entry.Branch),
                FileExists = Blank(entry.FileExists),
                Tag = Blank(entry.Tag),
                IsWorktree = entry.IsWorktree,
            };

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
