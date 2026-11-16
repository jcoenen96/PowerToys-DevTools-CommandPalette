using DevTools.Application.Abstractions;
using DevTools.Application.Git;
using DevTools.Application.Repositories;
using DevTools.Application.Tools;
using DevTools.Infrastructure.FileSystem;
using DevTools.Infrastructure.Git;
using DevTools.Infrastructure.Processes;
using DevTools.Infrastructure.Repositories;
using DevTools.Infrastructure.Tools;

namespace DevTools.Infrastructure;

/// <summary>
/// Wires the application services to their infrastructure. The extension builds one of these at startup; a DI
/// container would add reflection, which the trimmed/AOT build of the extension avoids.
/// </summary>
public sealed class DevToolsServices : IDisposable
{
    /// <param name="settings">The settings page.</param>
    /// <param name="catalog">The tools.</param>
    /// <param name="repositoryConfiguration">Tags, actions and searches; none when not given.</param>
    /// <param name="stateStore">Where pins, ignored repositories and usage are kept; in memory when not given.</param>
    /// <param name="time">The clock; the system clock when not given.</param>
    public DevToolsServices(
        ISettingsProvider settings,
        IToolCatalog catalog,
        IRepositoryConfiguration? repositoryConfiguration = null,
        IRepositoryStateStore? stateStore = null,
        TimeProvider? time = null
    )
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(catalog);
        time ??= TimeProvider.System;
        RepositoryConfiguration = repositoryConfiguration ?? new EmptyRepositoryConfiguration();
        Preferences = new RepositoryPreferences(stateStore ?? new InMemoryRepositoryStateStore(), time);

        Settings = settings;
        Catalog = catalog;
        var resolver = new ExecutableResolver(time);
        catalog.Changed += (_, _) =>
        {
            resolver.Clear();
            ExecutableIconLocator.Clear();
        };
        Resolver = resolver;

        var files = new FileSystemFileFinder();
        Repositories = new RepositoryService(new FileSystemRepositoryScanner(), settings, time, Preferences);
        Tools = new ToolService(catalog, settings, resolver);
        Targets = new LaunchTargetService(Repositories, files, settings);
        Launcher = new ToolLauncher(resolver, new ProcessLauncher());
        Shell = new ProcessLauncher();
        Git = new GitService(new GitProcessRunner(settings), settings, Repositories);
        RepositoryCatalog = new RepositoryCatalog(Repositories, Preferences, RepositoryConfiguration, settings);
        Status = new GitStatusCache(Git, time);
        AutoFetch = new AutoFetchService(Git, Repositories, settings, time);
        Actions = new RepositoryActionService(RepositoryConfiguration, files, settings, resolver, Shell);
    }

    /// <summary>Tags, custom actions and saved searches from repositories.json.</summary>
    public IRepositoryConfiguration RepositoryConfiguration { get; }

    /// <summary>Pinned and ignored repositories, usage and saved searches.</summary>
    public RepositoryPreferences Preferences { get; }

    /// <summary>The repositories with their tags and pins, sorted as the settings say.</summary>
    public RepositoryCatalog RepositoryCatalog { get; }

    public GitStatusCache Status { get; }

    public AutoFetchService AutoFetch { get; }

    public RepositoryActionService Actions { get; }

    public ISettingsProvider Settings { get; }

    public IToolCatalog Catalog { get; }

    public IExecutableResolver Resolver { get; }

    public RepositoryService Repositories { get; }

    public ToolService Tools { get; }

    public LaunchTargetService Targets { get; }

    public ToolLauncher Launcher { get; }

    public IProcessLauncher Shell { get; }

    public GitService Git { get; }

    public void Dispose()
    {
        AutoFetch.Dispose();
        (Catalog as IDisposable)?.Dispose();
        (RepositoryConfiguration as IDisposable)?.Dispose();
    }
}
