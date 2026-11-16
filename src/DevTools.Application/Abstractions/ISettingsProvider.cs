using DevTools.Domain.Settings;

namespace DevTools.Application.Abstractions;

public interface ISettingsProvider
{
    DevToolsSettings Current { get; }

    event EventHandler? Changed;
}
