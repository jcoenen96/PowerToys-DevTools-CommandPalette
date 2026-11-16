using DevTools.Domain.Tools;

namespace DevTools.Application.Abstractions;

/// <summary>The presets plus the tools the user added or overrode in <c>tools.json</c>.</summary>
public interface IToolCatalog
{
    /// <summary>The file with the user's own tools.</summary>
    string ConfigurationPath { get; }

    IReadOnlyList<ToolDefinition> GetTools();

    /// <summary>Why <c>tools.json</c> could not be read, if it couldn't; the presets still work then.</summary>
    string? ConfigurationError { get; }

    /// <summary>Writes an example <c>tools.json</c> when there is none yet.</summary>
    void EnsureConfigurationFile();

    event EventHandler? Changed;
}
