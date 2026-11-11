using DevTools.CommandPalette.Commands;
using DevTools.CommandPalette.Pages;
using DevTools.CommandPalette.Settings;
using DevTools.Infrastructure;
using DevTools.Infrastructure.Tools;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DevTools.CommandPalette;

/// <summary>
/// The top-level commands: "All dev tools", one "Open in &lt;tool&gt;" per enabled tool (each can get its own hotkey and alias in
/// Command Palette), "Git repositories", and "Edit dev tools". The list follows the settings and tools.json.
/// </summary>
public sealed partial class DevToolsCommandsProvider : CommandProvider
{
    private readonly SettingsManager _settings;
    private readonly JsonToolCatalog _catalog;
    private readonly DevToolsServices _services;
    private readonly Lock _lock = new();
    private ICommandItem[]? _commands;

    public DevToolsCommandsProvider()
    {
        Id = "DevTools";
        DisplayName = "Dev tools";
        Icon = Icons.Extension;

        var folder = Utilities.BaseSettingsPath("DevTools.CommandPalette");
        Directory.CreateDirectory(folder);

        _catalog = new JsonToolCatalog(Path.Combine(folder, "tools.json"));
        _settings = new SettingsManager(Path.Combine(folder, "settings.json"), _catalog.GetTools());
        _services = new DevToolsServices(_settings, _catalog);

        Settings = _settings.Settings;
        // The commands change with the settings and tools.json, so Command Palette must ask again.
        Frozen = false;

        _settings.Changed += (_, _) => Rebuild();
        _catalog.Changed += (_, _) =>
        {
            // A tool added to tools.json gets its own switch in the settings.
            _settings.EnsureToolToggles(_catalog.GetTools());
            Rebuild();
        };
    }

    public override ICommandItem[] TopLevelCommands()
    {
        lock (_lock)
        {
            return _commands ??= Build();
        }
    }

    public override void Dispose()
    {
        _services.Dispose();
        base.Dispose();
    }

    private void Rebuild()
    {
        lock (_lock)
        {
            _commands = null;
        }

        RaiseItemsChanged();
    }

    private ICommandItem[] Build()
    {
        var settingsItem = new CommandContextItem(_settings.Settings.SettingsPage) { Title = "Dev tools settings" };
        var editTools = new OpenWithShellCommand(
            _services,
            _catalog.ConfigurationPath,
            "Edit tools",
            Icons.Edit,
            _catalog.EnsureConfigurationFile
        )
        {
            Id = CommandIds.EditTools,
        };

        // Every enabled tool gets its own command, so it can have its own hotkey and alias. A preset that isn't
        // installed is left out; a tool from tools.json stays, with a warning, so its command and hotkey don't vanish.
        var tools = _services
            .Tools.GetCommandTools()
            .Select(entry =>
            {
                var item = new CommandItem(new ToolTargetsPage(_services, entry.Tool))
                {
                    Title = $"Open in {entry.Tool.DisplayName}",
                    Subtitle =
                        !entry.IsInstalled ? "Not found: check its executables in tools.json"
                        : entry.Tool.TargetKind == Domain.Tools.ToolTargetKind.Files
                            ? "Solutions in your repositories and worktrees, with their branch"
                        : "Your repositories and worktrees, with their branch",
                    MoreCommands = [settingsItem],
                };
                if (!entry.IsInstalled)
                {
                    item.Icon = Icons.Warning;
                }

                return (ICommandItem)item;
            });

        return
        [
            new CommandItem(new AllToolsPage(_services))
            {
                Title = "All dev tools",
                Subtitle = "Every tool's solutions and repositories on one page, with their branch",
                MoreCommands = [settingsItem],
            },
            .. tools,
            new CommandItem(new RepositoriesPage(_services))
            {
                Title = "Git repositories",
                Subtitle = "Branches, worktrees, fetch, pull, push and more",
                MoreCommands = [settingsItem],
            },
            new CommandItem(editTools)
            {
                Title = "Edit dev tools",
                Subtitle = _catalog.ConfigurationError ?? "Add your own tools or change the presets in tools.json",
                Icon = _catalog.ConfigurationError is null ? Icons.Edit : Icons.Warning,
                MoreCommands = [settingsItem],
            },
        ];
    }
}
