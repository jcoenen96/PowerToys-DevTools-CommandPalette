using DevTools.CommandPalette.Commands;
using DevTools.Domain.Git;
using DevTools.Infrastructure;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DevTools.CommandPalette.Pages;

internal enum BranchAction
{
    Checkout,
    AddWorktree,
}

/// <summary>Local and remote branches of a repository, to switch to or to open in a new worktree.</summary>
internal sealed partial class BranchesPage : ListPage
{
    private readonly DevToolsServices _services;
    private readonly GitRepository _repository;
    private readonly BranchAction _action;

    public BranchesPage(DevToolsServices services, GitRepository repository, BranchAction action)
    {
        _services = services;
        _repository = repository;
        _action = action;
        Name = action == BranchAction.Checkout ? "Switch branch" : "New worktree";
        Title =
            action == BranchAction.Checkout
                ? $"Switch branch in {repository.Name}"
                : $"New worktree for a branch of {repository.Name}";
        Icon = action == BranchAction.Checkout ? Icons.Branch : Icons.Worktree;
        PlaceholderText = "Search branches";
    }

    public override IListItem[] GetItems()
    {
        IsLoading = true;
        try
        {
            var branches = _services.Git.ListBranchesAsync(_repository).GetAwaiter().GetResult();
            return [.. branches.Select(Item)];
        }
        finally
        {
            IsLoading = false;
        }
    }

    private ListItem Item(GitBranch branch)
    {
        var elsewhere =
            branch.WorktreePath is { } path
            && !string.Equals(
                Path.GetFullPath(path),
                Path.GetFullPath(_repository.Path),
                StringComparison.OrdinalIgnoreCase
            );

        return new ListItem(Command(branch))
        {
            Title = branch.Name,
            Subtitle = branch switch
            {
                { IsCurrent: true } => "Checked out here",
                _ when elsewhere => $"Checked out in {branch.WorktreePath}",
                { IsRemote: true } => $"Remote branch; creates the local branch {branch.LocalName}",
                _ => "Local branch",
            },
            Icon = branch.IsRemote ? new IconInfo("\uE753") : Icons.Branch,
            Tags = elsewhere ? [new Tag("worktree") { Icon = Icons.Worktree }] : [],
        };
    }

    private BackgroundCommand Command(GitBranch branch) =>
        _action == BranchAction.Checkout
            ? new BackgroundCommand(
                $"Switch to {branch.LocalName}",
                Icons.Branch,
                $"Switching {_repository.Name} to {branch.LocalName}…",
                async () =>
                {
                    _services.Preferences.RecordUse(_repository.Path);
                    var result = await _services.Git.CheckoutAsync(_repository, branch).ConfigureAwait(false);
                    return (
                        result.Succeeded,
                        result.Succeeded
                            ? $"{_repository.Name} is now on {branch.LocalName}"
                            : $"Could not switch to {branch.LocalName}: {result.Summary}"
                    );
                }
            )
            {
                Result = CommandResult.GoHome,
            }
            : new BackgroundCommand(
                $"New worktree for {branch.LocalName}",
                Icons.Worktree,
                $"Creating a worktree for {branch.LocalName}…",
                async () =>
                {
                    var result = await _services.Git.AddWorktreeAsync(_repository, branch).ConfigureAwait(false);
                    return (result.Succeeded, result.Message);
                }
            )
            {
                Result = CommandResult.GoHome,
            };
}
