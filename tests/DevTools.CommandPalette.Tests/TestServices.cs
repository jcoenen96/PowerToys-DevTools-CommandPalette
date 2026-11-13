using DevTools.Application.Abstractions;
using DevTools.Domain.Settings;
using DevTools.Domain.Tools;
using DevTools.Infrastructure;
using DevTools.Infrastructure.Repositories;
using DevTools.Infrastructure.Tests;

namespace DevTools.CommandPalette.Tests;

/// <summary>
/// The extension's services over a temp folder with real repositories: <c>repos/Shop</c> (with
/// <c>Shop.slnx</c>) and its worktree on <c>feature/cart</c>, and <c>repos/Api</c>.
/// </summary>
internal sealed class TestServices : IDisposable
{
    // cmd.exe is on PATH on every Windows machine, so both tools count as installed.
    public static readonly ToolDefinition SolutionTool = new()
    {
        Id = "solutions",
        DisplayName = "Solution tool",
        Executables = ["cmd.exe"],
        TargetKind = ToolTargetKind.Files,
        FilePatterns = ["*.slnx", "*.sln"],
        FallBackToFolder = true,
    };

    public static readonly ToolDefinition FolderTool = new()
    {
        Id = "folders",
        DisplayName = "Folder tool",
        Executables = ["cmd.exe"],
    };

    private readonly TempFolder _temp = new();

    public TestServices(bool groupByProject = true)
    {
        var shop = _temp.Repository("repos/Shop");
        _temp.File("repos/Shop/Shop.slnx", "<Solution />");
        TempFolder.Git(shop, "add", ".");
        TempFolder.Git(
            shop,
            "-c",
            "user.name=Test",
            "-c",
            "user.email=test@example.com",
            "commit",
            "--quiet",
            "-m",
            "sln"
        );
        _temp.Repository("repos/Api");
        TempFolder.Git(
            shop,
            "worktree",
            "add",
            "--quiet",
            "-b",
            "feature/cart",
            _temp.Combine("worktrees", "Shop-cart")
        );

        Settings = new StaticSettings(
            new DevToolsSettings
            {
                RepositoryRoots = [_temp.Combine("repos")],
                WorktreeRoots = [_temp.Combine("worktrees")],
                GroupByProject = groupByProject,
            }
        );
        TempFolder.Git(shop, "remote", "add", "origin", "https://github.com/me/shop.git");
        Services = new DevToolsServices(Settings, new StaticCatalog(_temp.Combine("tools.json")), Configuration);
    }

    public StaticSettings Settings { get; }

    /// <summary>Tags, actions and searches, as repositories.json would give them.</summary>
    public EmptyRepositoryConfiguration Configuration { get; } = new();

    public DevToolsServices Services { get; }

    public void Dispose()
    {
        Services.Dispose();
        _temp.Dispose();
    }

    internal sealed class StaticSettings(DevToolsSettings settings) : ISettingsProvider
    {
        public DevToolsSettings Current { get; set; } = settings;

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }
    }

    private sealed class StaticCatalog(string path) : IToolCatalog
    {
        public string ConfigurationPath => path;

        public string? ConfigurationError => null;

        public IReadOnlyList<ToolDefinition> GetTools() => [SolutionTool, FolderTool];

        public void EnsureConfigurationFile() { }

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }
    }
}
