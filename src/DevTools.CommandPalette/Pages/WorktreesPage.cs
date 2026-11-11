using DevTools.Application.Git;
using DevTools.CommandPalette.Commands;
using DevTools.Domain.Git;
using DevTools.Infrastructure;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DevTools.CommandPalette.Pages;

/// <summary>The worktrees of a repository; open one, or remove it from the more commands menu.</summary>
internal sealed partial class WorktreesPage : ListPage
{
    private readonly DevToolsServices _services;
    private readonly GitRepository _repository;

    public WorktreesPage(DevToolsServices services, GitRepository repository)
    {
        _services = services;
        _repository = repository;
        Name = "Worktrees";
        Title = $"Worktrees of {repository.Name}";
        Icon = Icons.Worktree;
        PlaceholderText = "Search worktrees";
    }

    public override IListItem[] GetItems()
    {
        IsLoading = true;
        try
        {
            var installed = _services.Tools.GetInstalledTools();
            var worktrees = _services.Git.ListWorktreesAsync(_repository).GetAwaiter().GetResult();
            return
            [
                .. worktrees.Select(worktree =>
                {
                    var repository = new GitRepository(
                        _repository.Name,
                        worktree.Path,
                        worktree.Head,
                        worktree.IsMain ? RepositoryKind.Clone : RepositoryKind.Worktree
                    );

                    var menu = RepositoryMenu.For(_services, repository, installed, () => RaiseItemsChanged()).ToList();
                    if (!worktree.IsMain)
                    {
                        menu.Add(new CommandContextItem(RemoveCommand(worktree)) { IsCritical = true });
                    }

                    return new ListItem(new RepositoryPage(_services, repository))
                    {
                        Title = $"{_repository.Name} ({worktree.Head.DisplayName})",
                        Subtitle = worktree.Path,
                        Icon = Icons.For(repository),
                        Tags = worktree switch
                        {
                            { IsMain: true } => [new Tag("main worktree")],
                            { IsPrunable: true } => [new Tag("missing; prune it")],
                            { IsLocked: true } => [new Tag("locked")],
                            _ => [],
                        },
                        MoreCommands = [.. menu],
                    };
                }),
                new ListItem(GitCommandFactory.Create(_services, _repository, GitCommands.PruneWorktrees))
                {
                    Title = GitCommands.PruneWorktrees.Title,
                    Subtitle = "Forget worktrees whose folder was deleted",
                    Icon = new IconInfo(GitCommands.PruneWorktrees.Icon),
                },
            ];
        }
        finally
        {
            IsLoading = false;
        }
    }

    private AnonymousCommand RemoveCommand(GitWorktree worktree)
    {
        var remove = new BackgroundCommand(
            "Remove worktree",
            Icons.Delete,
            $"Removing the worktree in {worktree.Path}…",
            async () =>
            {
                var result = await _services.Git.RemoveWorktreeAsync(_repository, worktree).ConfigureAwait(false);
                return (result.Succeeded, result.Succeeded ? $"Removed {worktree.Path}" : result.Summary);
            },
            _ => RaiseItemsChanged()
        );

        return new AnonymousCommand(() => { })
        {
            Name = "Remove worktree",
            Icon = Icons.Delete,
            Result = CommandResult.Confirm(
                new ConfirmationArgs
                {
                    Title = $"Remove the worktree {worktree.Head.DisplayName}?",
                    Description =
                        $"Runs git worktree remove {worktree.Path}. Git refuses when it has uncommitted changes; the branch stays.",
                    PrimaryCommand = remove,
                    IsPrimaryCommandCritical = true,
                }
            ),
        };
    }
}
