using DevTools.Application.Abstractions;
using DevTools.Domain.Settings;

namespace DevTools.Application.Tests;

internal sealed class FakeSettings(DevToolsSettings? settings = null) : ISettingsProvider
{
    public DevToolsSettings Current { get; private set; } = settings ?? new DevToolsSettings();

    public event EventHandler? Changed;

    public void Set(DevToolsSettings settings)
    {
        Current = settings;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
