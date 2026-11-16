using DevTools.Application.Git;
using DevTools.CommandPalette.Commands;
using DevTools.Domain.Git;
using DevTools.Domain.Projects;
using DevTools.Domain.Tools;
using DevTools.Infrastructure;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DevTools.CommandPalette.Pages;

/// <summary>Everything for one repository or worktree: git commands first, then branches, worktrees and opening it in a tool.</summary>
internal sealed partial class RepositoryPage : ListPage
{
    private readonly DevToolsServices _services;
    private readonly GitRepository _repository;

    public RepositoryPage(DevToolsServices services, GitRepository repository)
    {
        _services = services;
        _repository = repository;
        Name = "Git commands";
        Title = $"{repository.Name} ({repository.Head.DisplayName})";
        Icon = Icons.For(repository);
        PlaceholderText = "Search git commands and tools";
    }

    public override IListItem[] GetItems()
    {
        IsLoading = true;
        try
        {
            return
            [
                StatusItem(),
                .. CommandItems(),
                .. BranchItems(),
                .. Extras(RepositoryExtras.ActionItems(_services, _repository)),
                .. Extras(RepositoryExtras.RemoteItems(_repository)),
                .. ToolItems(),
                .. Extras(RepositoryExtras.ManageItems(_services, _repository, () => RaiseItemsChanged())),
            ];
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static IEnumerable<ListItem> Extras(IEnumerable<RepositoryExtras.Extra> extras) =>
        extras.Select(RepositoryExtras.ToListItem);

    private ListItem StatusItem()
    {
        var status = _services.Git.GetStatusAsync(_repository).GetAwaiter().GetResult();
        return new ListItem(new GitOutputPage(_services, _repository, GitCommands.Status))
        {
            Title = $"On {_repository.Head.DisplayName}: {status?.Summary ?? "status unknown"}",
            Subtitle = status?.Upstream is { } upstream ? $"Tracking {upstream}" : "No upstream branch",
            Icon = status is null ? Icons.Warning : Icons.Branch,
            Section = "Status",
        };
    }

    private IEnumerable<ListItem> ToolItems()
    {
        foreach (var tool in _services.Tools.GetInstalledTools())
        {
            var targets =
                tool.TargetKind == ToolTargetKind.Folder
                    ? (LaunchTarget[])[new LaunchTarget(tool, _repository, _repository.Path, IsFolder: true)]
                    : _services.Targets.GetTargets(tool, _repository);

            foreach (var target in targets)
            {
                yield return new ListItem(new LaunchCommand(_services, target))
                {
                    Title = $"Open {target.Name} in {tool.DisplayName}",
                    Subtitle = target.Path,
                    Section = "Open",
                };
            }
        }
    }

    private IEnumerable<ListItem> BranchItems()
    {
        yield return Item(
            new BranchesPage(_services, _repository, BranchAction.Checkout),
            "Switch branch",
            "git switch",
            Icons.Branch
        );
        yield return Item(
            new NewBranchPage(_services, _repository, inNewWorktree: false),
            "New branch",
            "git switch -c",
            Icons.NewBranch
        );
        yield return Item(
            new BranchesPage(_services, _repository, BranchAction.AddWorktree),
            "New worktree for a branch",
            "git worktree add, in your worktree folder",
            Icons.Worktree
        );
        yield return Item(
            new NewBranchPage(_services, _repository, inNewWorktree: true),
            "New branch in a new worktree",
            "git worktree add -b, in your worktree folder",
            Icons.Worktree
        );
        yield return Item(new WorktreesPage(_services, _repository), "Worktrees", "git worktree list", Icons.Worktree);
    }

    private IEnumerable<ListItem> CommandItems() =>
        GitCommands
            .All.Where(command => command != GitCommands.Status)
            .Select(command => new ListItem(GitCommandFactory.For(_services, _repository, command))
            {
                Title = command.Title,
                Subtitle = command.Description,
                Icon = new IconInfo(command.Icon),
                Section = "Git",
            });

    private static ListItem Item(ICommand command, string title, string subtitle, IconInfo icon) =>
        new(command)
        {
            Title = title,
            Subtitle = subtitle,
            Icon = icon,
            Section = "Branches and worktrees",
        };
}
