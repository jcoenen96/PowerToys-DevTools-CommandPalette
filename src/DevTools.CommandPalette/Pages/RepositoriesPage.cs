using DevTools.Infrastructure;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DevTools.CommandPalette.Pages;

/// <summary>All clones and worktrees with their branch; choosing one opens its git commands.</summary>
internal sealed partial class RepositoriesPage : ListPage
{
    private readonly DevToolsServices _services;
    private bool _refresh;

    public RepositoriesPage(DevToolsServices services)
    {
        _services = services;
        Id = CommandIds.Repositories;
        Name = "Open";
        Title = "Git repositories";
        Icon = Icons.Repository;
        PlaceholderText = "Search repositories, worktrees and branches";
        EmptyContent = EmptyStates.NoRepositories(services);
    }

    public override IListItem[] GetItems()
    {
        IsLoading = true;
        try
        {
            var refresh = _refresh;
            _refresh = false;
            var installed = _services.Tools.GetInstalledTools();

            return
            [
                .. _services
                    .Repositories.GetRepositories(refresh)
                    .Select(repository => new ListItem(new RepositoryPage(_services, repository))
                    {
                        Title = $"{repository.Name} ({repository.Head.DisplayName})",
                        Subtitle = repository.Path,
                        Icon = Icons.For(repository),
                        Tags = Tags.For(repository),
                        MoreCommands = RepositoryMenu.For(_services, repository, installed, Refresh),
                    }),
            ];
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void Refresh()
    {
        _refresh = true;
        RaiseItemsChanged();
    }
}
