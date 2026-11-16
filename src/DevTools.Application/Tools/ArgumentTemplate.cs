using DevTools.Domain.Projects;
using DevTools.Domain.Tools;

namespace DevTools.Application.Tools;

/// <summary>Fills in the placeholders of a tool's arguments.</summary>
public static class ArgumentTemplate
{
    public static string Render(string template, LaunchTarget target)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(target);

        return template
            .Replace(ArgumentPlaceholders.Target, target.Path, StringComparison.OrdinalIgnoreCase)
            .Replace(ArgumentPlaceholders.Folder, target.Folder, StringComparison.OrdinalIgnoreCase)
            .Replace(ArgumentPlaceholders.Repository, target.Repository.Path, StringComparison.OrdinalIgnoreCase)
            .Replace(
                ArgumentPlaceholders.Branch,
                target.Repository.Head.DisplayName,
                StringComparison.OrdinalIgnoreCase
            )
            .Replace(ArgumentPlaceholders.Name, target.Name, StringComparison.OrdinalIgnoreCase);
    }
}
