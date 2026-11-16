using DevTools.Domain.Projects;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DevTools.CommandPalette.Pages;

/// <summary>
/// One project's clone and worktrees, each with its branch, the clone first: <c>Shop.slnx (main)</c>, then
/// <c>Shop.slnx (feature/cart)</c>. Opened from a tool's list of projects.
/// </summary>
internal sealed partial class ProjectTargetsPage : ListPage
{
    private readonly IListItem[] _items;

    public ProjectTargetsPage(Project project, string title, IconInfo icon, IListItem[] items)
    {
        _items = items;
        Name = "Choose branch";
        Title = title;
        Icon = icon;
        PlaceholderText = $"Search the branches and worktrees of {project.Name}";
    }

    public override IListItem[] GetItems() => _items;
}
