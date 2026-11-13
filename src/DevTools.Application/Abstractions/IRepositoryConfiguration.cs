using DevTools.Domain.Repositories;

namespace DevTools.Application.Abstractions;

/// <summary>The user's tags, custom actions and saved searches (<c>repositories.json</c>).</summary>
public interface IRepositoryConfiguration
{
    /// <summary>The file the configuration is read from.</summary>
    string ConfigurationPath { get; }

    RepositoryConfiguration Current { get; }

    /// <summary>Why the file could not be read, if it couldn't; everything else keeps working then.</summary>
    string? ConfigurationError { get; }

    /// <summary>Writes an example file when there is none yet.</summary>
    void EnsureConfigurationFile();

    event EventHandler? Changed;
}
