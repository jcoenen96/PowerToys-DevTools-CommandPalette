using DevTools.Application.Abstractions;
using DevTools.Application.Git;
using DevTools.Application.Repositories;
using DevTools.Application.Tools;
using DevTools.Infrastructure.FileSystem;
using DevTools.Infrastructure.Git;
using DevTools.Infrastructure.Processes;
using DevTools.Infrastructure.Tools;

namespace DevTools.Infrastructure;

/// <summary>
/// Wires the application services to their infrastructure. The extension builds one of these at startup; a DI
/// container would add reflection, which the trimmed/AOT build of the extension avoids.
/// </summary>
public sealed class DevToolsServices : IDisposable
{
    public DevToolsServices(ISettingsProvider settings, IToolCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(catalog);
        var time = TimeProvider.System;

        Settings = settings;
        Catalog = catalog;
        var resolver = new ExecutableResolver(time);
        catalog.Changed += (_, _) =>
        {
            resolver.Clear();
            ExecutableIconLocator.Clear();
        };
        Resolver = resolver;

        Repositories = new RepositoryService(new FileSystemRepositoryScanner(), settings, time);
        Tools = new ToolService(catalog, settings, resolver);
        Targets = new LaunchTargetService(Repositories, new FileSystemFileFinder(), settings);
        Launcher = new ToolLauncher(resolver, new ProcessLauncher());
        Shell = new ProcessLauncher();
        Git = new GitService(new GitProcessRunner(settings), settings, Repositories);
    }

    public ISettingsProvider Settings { get; }

    public IToolCatalog Catalog { get; }

    public IExecutableResolver Resolver { get; }

    public RepositoryService Repositories { get; }

    public ToolService Tools { get; }

    public LaunchTargetService Targets { get; }

    public ToolLauncher Launcher { get; }

    public IProcessLauncher Shell { get; }

    public GitService Git { get; }

    public void Dispose() => (Catalog as IDisposable)?.Dispose();
}
