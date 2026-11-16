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

    /// <summary>
    /// The tools that get their own top-level command: every enabled tool that is installed, plus enabled tools from
    /// tools.json that were not found, so a typo in their path shows up instead of the tool silently missing.
    /// </summary>
    public IReadOnlyList<(ToolDefinition Tool, bool IsInstalled)> GetCommandTools() =>
        [
            .. GetEnabledTools()
                .Select(tool => (tool, !string.IsNullOrEmpty(resolver.Resolve(tool.Executables))))
                .Where(entry => entry.Item2 || !entry.tool.IsPreset),
        ];

    public ToolDefinition? Find(string id) =>
        catalog.GetTools().FirstOrDefault(tool => string.Equals(tool.Id, id, StringComparison.OrdinalIgnoreCase));
}
