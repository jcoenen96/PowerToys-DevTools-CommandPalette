using DevTools.Application.Repositories;
using DevTools.Domain.Git;
using DevTools.Domain.Repositories;
using DevTools.Infrastructure;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DevTools.CommandPalette.Commands;

/// <summary>Pins a repository (listed first, found with <c>is:pinned</c>) or unpins it. Pins are kept across restarts.</summary>
internal sealed partial class PinCommand : InvokableCommand
{
    private readonly DevToolsServices _services;
    private readonly GitRepository _repository;
    private readonly Action _refresh;

    public PinCommand(DevToolsServices services, GitRepository repository, Action refresh)
    {
        _services = services;
        _repository = repository;
        _refresh = refresh;
        var pinned = services.Preferences.IsPinned(repository.Path);
        Name = pinned ? "Unpin" : "Pin";
        Icon = pinned ? Icons.Unpin : Icons.Pin;
    }

    public override ICommandResult Invoke()
    {
        var pinned = _services.Preferences.TogglePin(_repository.Path);
        _refresh();
        Toasts.Show(true, pinned ? $"Pinned {_repository.Name}" : $"Unpinned {_repository.Name}");
        return CommandResult.KeepOpen();
    }
}

/// <summary>Hides a repository from every list; Git repositories > Ignored repositories brings it back.</summary>
internal sealed partial class IgnoreCommand : InvokableCommand
{
    private readonly DevToolsServices _services;
    private readonly GitRepository _repository;
    private readonly Action _refresh;

    public IgnoreCommand(DevToolsServices services, GitRepository repository, Action refresh)
    {
        _services = services;
        _repository = repository;
        _refresh = refresh;
        Name = "Ignore this repository";
        Icon = Icons.Hide;
    }

    public override ICommandResult Invoke()
    {
        _services.Preferences.SetIgnored(_repository.Path, ignored: true);
        _refresh();
        Toasts.Show(true, $"{_repository.Name} is hidden. Git repositories > Ignored repositories shows it again.");
        return CommandResult.KeepOpen();
    }
}

/// <summary>Shows an ignored repository again.</summary>
internal sealed partial class UnignoreCommand : InvokableCommand
{
    private readonly DevToolsServices _services;
    private readonly string? _path;
    private readonly Action _refresh;

    /// <param name="services">The services.</param>
    /// <param name="path">The repository to show again; <see langword="null"/> shows every ignored repository again.</param>
    /// <param name="refresh">Reloads the list.</param>
    public UnignoreCommand(DevToolsServices services, string? path, Action refresh)
    {
        _services = services;
        _path = path;
        _refresh = refresh;
        Name = path is null ? "Show all again" : "Show again";
        Icon = Icons.Show;
    }

    public override ICommandResult Invoke()
    {
        if (_path is null)
        {
            _services.Preferences.ClearIgnored();
        }
        else
        {
            _services.Preferences.SetIgnored(_path, ignored: false);
        }

        _services.Repositories.Invalidate();
        _refresh();
        return CommandResult.KeepOpen();
    }
}

/// <summary>A custom action from repositories.json: opens a URL, starts a command, or copies text.</summary>
internal sealed partial class RepositoryActionCommand : InvokableCommand
{
    private readonly DevToolsServices _services;
    private readonly GitRepository _repository;
    private readonly ResolvedRepositoryAction _action;

    public RepositoryActionCommand(DevToolsServices services, GitRepository repository, ResolvedRepositoryAction action)
    {
        _services = services;
        _repository = repository;
        _action = action;
        Name = action.Name;
        Icon = IconFor(action);
    }

    public static IconInfo IconFor(ResolvedRepositoryAction action) =>
        !string.IsNullOrWhiteSpace(action.Icon)
            ? new IconInfo(action.Icon)
            : action.Kind switch
            {
                RepositoryActionKind.Url => Icons.Web,
                RepositoryActionKind.Copy => Icons.Copy,
                _ => Icons.Run,
            };

    public override ICommandResult Invoke()
    {
        _services.Preferences.RecordUse(_repository.Path);
        if (_action.Kind == RepositoryActionKind.Copy)
        {
            ClipboardHelper.SetText(_action.Target);
            Toasts.Show(true, $"Copied {_action.Target}");
            return CommandResult.KeepOpen();
        }

        var result = _services.Actions.Run(_action);
        if (result.Succeeded)
        {
            return CommandResult.Dismiss();
        }

        Toasts.Show(false, result.Message);
        return CommandResult.KeepOpen();
    }
}

/// <summary>Keeps the current search on the Git repositories page and in its filter list.</summary>
internal sealed partial class SaveSearchCommand : InvokableCommand
{
    private readonly DevToolsServices _services;
    private readonly string _query;
    private readonly Action _refresh;

    public SaveSearchCommand(DevToolsServices services, string query, Action refresh)
    {
        _services = services;
        _query = query;
        _refresh = refresh;
        Name = "Save search";
        Icon = Icons.Save;
    }

    public override ICommandResult Invoke()
    {
        _services.Preferences.SaveSearch(_query, _query);
        _refresh();
        Toasts.Show(true, $"Saved the search \"{_query}\"");
        return CommandResult.KeepOpen();
    }
}

internal sealed partial class DeleteSearchCommand : InvokableCommand
{
    private readonly DevToolsServices _services;
    private readonly SavedSearch _search;
    private readonly Action _refresh;

    public DeleteSearchCommand(DevToolsServices services, SavedSearch search, Action refresh)
    {
        _services = services;
        _search = search;
        _refresh = refresh;
        Name = "Delete saved search";
        Icon = Icons.Delete;
    }

    public override ICommandResult Invoke()
    {
        _services.Preferences.DeleteSearch(_search);
        _refresh();
        return CommandResult.KeepOpen();
    }
}
