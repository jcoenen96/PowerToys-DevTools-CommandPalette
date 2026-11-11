using DevTools.Application.Abstractions;
using DevTools.Domain.Tools;

namespace DevTools.Application.Tools;

/// <summary>The tools the user has switched on, and whether each is installed.</summary>
public sealed class ToolService(IToolCatalog catalog, ISettingsProvider settings, IExecutableResolver resolver)
{
    public IReadOnlyList<ToolDefinition> GetEnabledTools()
    {
        var disabled = settings.Current.DisabledToolIds;
        return [.. catalog.GetTools().Where(tool => !disabled.Contains(tool.Id))];
    }

    /// <summary>The enabled tools that are installed on this machine.</summary>
    public IReadOnlyList<ToolDefinition> GetInstalledTools() =>
        [.. GetEnabledTools().Where(tool => !string.IsNullOrEmpty(resolver.Resolve(tool.Executables)))];

    public ToolDefinition? Find(string id) =>
        catalog.GetTools().FirstOrDefault(tool => string.Equals(tool.Id, id, StringComparison.OrdinalIgnoreCase));
}
