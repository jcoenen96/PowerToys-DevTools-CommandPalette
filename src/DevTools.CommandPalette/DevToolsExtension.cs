using System.Runtime.InteropServices;
using Microsoft.CommandPalette.Extensions;

namespace DevTools.CommandPalette;

/// <summary>The COM entry point Command Palette activates; the GUID matches the CLSID in Package.appxmanifest.</summary>
[Guid("026DEDAD-0F1D-4686-9E39-631CA5CC5DBA")]
public sealed partial class DevToolsExtension : IExtension, IDisposable
{
    private readonly ManualResetEvent _extensionDisposedEvent;
    private readonly DevToolsCommandsProvider _provider = new();

    public DevToolsExtension(ManualResetEvent extensionDisposedEvent)
    {
        _extensionDisposedEvent = extensionDisposedEvent;
    }

    public object? GetProvider(ProviderType providerType) =>
        providerType switch
        {
            ProviderType.Commands => _provider,
            _ => null,
        };

    public void Dispose()
    {
        _provider.Dispose();
        _extensionDisposedEvent.Set();
    }
}
