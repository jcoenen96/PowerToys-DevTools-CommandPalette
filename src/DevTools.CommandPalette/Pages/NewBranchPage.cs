using DevTools.CommandPalette.Commands;
using DevTools.Domain.Git;
using DevTools.Infrastructure;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DevTools.CommandPalette.Pages;

/// <summary>Type a branch name in the search box, then press Enter to create it (here, or in a new worktree).</summary>
internal sealed partial class NewBranchPage : DynamicListPage
{
    private readonly DevToolsServices _services;
    private readonly GitRepository _repository;
    private readonly bool _inNewWorktree;

    public NewBranchPage(DevToolsServices services, GitRepository repository, bool inNewWorktree)
    {
        _services = services;
        _repository = repository;
        _inNewWorktree = inNewWorktree;
        Name = inNewWorktree ? "New branch in a new worktree" : "New branch";
        Title = $"{Name} ({repository.Name}, from {repository.Head.DisplayName})";
        Icon = inNewWorktree ? Icons.Worktree : Icons.NewBranch;
        PlaceholderText = "Type the new branch name, e.g. feature/login";
    }

    public override void UpdateSearchText(string oldSearch, string newSearch) => RaiseItemsChanged();

    public override IListItem[] GetItems()
    {
        var name = SearchText.Trim();
        if (name.Length == 0)
        {
            return [Hint("Type the name of the new branch", $"It starts from {_repository.Head.DisplayName}")];
        }

        if (!BranchNames.IsValid(name))
        {
            return [Hint($"'{name}' is not a valid branch name", "No spaces, '..', '~', '^', ':', '?', '*' or '['")];
        }

        var command = _inNewWorktree
            ? new BackgroundCommand(
                $"Create {name} in a new worktree",
                Icons.Worktree,
                $"Creating {name} in a new worktree…",
                async () =>
                {
                    var result = await _services
                        .Git.AddWorktreeForNewBranchAsync(_repository, name)
                        .ConfigureAwait(false);
                    return (result.Succeeded, result.Message);
                }
            )
            {
                Result = CommandResult.GoHome,
            }
            : new BackgroundCommand(
                $"Create {name}",
                Icons.NewBranch,
                $"Creating {name}…",
                async () =>
                {
                    var result = await _services.Git.CreateBranchAsync(_repository, name).ConfigureAwait(false);
                    return (
                        result.Succeeded,
                        result.Succeeded ? $"{_repository.Name} is now on {name}" : result.Summary
                    );
                }
            )
            {
                Result = CommandResult.GoHome,
            };

        return
        [
            new ListItem(command)
            {
                Title = _inNewWorktree ? $"Create {name} in a new worktree" : $"Create and switch to {name}",
                Subtitle = _inNewWorktree
                    ? Application.Git.WorktreePathBuilder.Build(_services.Settings.Current, _repository, name)
                        ?? "Set a worktree folder in the Dev tools settings first"
                    : $"From {_repository.Head.DisplayName} in {_repository.Path}",
                Icon = command.Icon,
            },
        ];
    }

    private static ListItem Hint(string title, string subtitle) =>
        new(new NoOpCommand())
        {
            Title = title,
            Subtitle = subtitle,
            Icon = Icons.Info,
        };
}
