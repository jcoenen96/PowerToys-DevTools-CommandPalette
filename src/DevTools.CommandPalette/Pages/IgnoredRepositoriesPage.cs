using DevTools.CommandPalette.Commands;
using DevTools.Infrastructure;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DevTools.CommandPalette.Pages;

/// <summary>The repositories hidden with "Ignore this repository"; choosing one shows it again.</summary>
internal sealed partial class IgnoredRepositoriesPage : ListPage
{
    private readonly DevToolsServices _services;

    public IgnoredRepositoriesPage(DevToolsServices services)
    {
        _services = services;
        Name = "Open";
        Title = "Ignored repositories";
        Icon = Icons.Hide;
        PlaceholderText = "Search ignored repositories";
        EmptyContent = new CommandItem(new NoOpCommand())
        {
            Title = "No ignored repositories",
            Subtitle = "Ctrl+K > Ignore this repository on a repository hides it from every list.",
            Icon = Icons.Info,
        };
    }

    public override IListItem[] GetItems()
    {
        var paths = _services.Preferences.IgnoredPaths;
        if (paths.Count == 0)
        {
            return [];
        }

        return
        [
            .. paths
                .Order(StringComparer.OrdinalIgnoreCase)
                .Select(path => new ListItem(new UnignoreCommand(_services, path, () => RaiseItemsChanged()))
                {
                    Title = Path.GetFileName(path),
                    Subtitle = path,
                    Icon = Icons.Hide,
                }),
            new ListItem(new UnignoreCommand(_services, null, () => RaiseItemsChanged()))
            {
                Title = "Show all ignored repositories again",
                Icon = Icons.Show,
            },
        ];
    }
}
