using System.Runtime.InteropServices;
using Microsoft.CommandPalette.Extensions;
using Windows.ApplicationModel.AppExtensions;
using WinRT;

namespace DevTools.CommandPalette.Tests;

/// <summary>Only runs when <c>DEVTOOLS_PACKAGED_TESTS=1</c>: the extension must be installed (Deploy-Extension.ps1).</summary>
public sealed class PackagedFactAttribute : FactAttribute
{
    public PackagedFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("DEVTOOLS_PACKAGED_TESTS") != "1")
        {
            Skip =
                "Needs the installed extension: run scripts/Deploy-Extension.ps1, then set DEVTOOLS_PACKAGED_TESTS=1.";
        }
    }
}

/// <summary>
/// The installed extension, reached the way Command Palette reaches it: found in the app extension catalog,
/// activated out of process over COM, then asked for its commands and pages, including a search.
/// </summary>
public sealed partial class PackagedExtensionTests
{
    private const string PackageName = "DevTools.CommandPalette";
    private static readonly Guid ExtensionClassId = new("026DEDAD-0F1D-4686-9E39-631CA5CC5DBA");

    [PackagedFact]
    public static async Task Command_Palette_finds_the_extension_once()
    {
        var catalog = AppExtensionCatalog.Open("com.microsoft.commandpalette");

        var extensions = await catalog.FindAllAsync();

        Assert.Single(extensions, extension => extension.Package.Id.Name == PackageName);
    }

    [PackagedFact]
    public static void The_installed_extension_lists_and_searches_over_COM()
    {
        var extension = Activate();
        try
        {
            // GetProvider returns a plain IInspectable across the process boundary; ask it for the interface.
            var inspectable = Assert.IsAssignableFrom<IInspectable>(extension.GetProvider(ProviderType.Commands));
            var provider = MarshalInterface<ICommandProvider>.FromAbi(inspectable.ThisPtr);
            var commands = provider.TopLevelCommands();
            Assert.Contains(commands, command => command.Command.Id == "devtools.all");

            foreach (var command in commands)
            {
                if (command.Command is IDynamicListPage page)
                {
                    page.GetItems();
                    page.SearchText = "a";
                    page.GetItems();
                    page.SearchText = "";
                }
                else if (command.Command is IListPage list)
                {
                    list.GetItems();
                }
            }
        }
        finally
        {
            // Lets the extension's process exit, as when Command Palette closes it.
            extension.Dispose();
        }
    }

    private static IExtension Activate()
    {
        var hr = CoCreateInstance(ExtensionClassId, IntPtr.Zero, ClsctxLocalServer, IUnknown, out var pointer);
        Marshal.ThrowExceptionForHR(hr);
        try
        {
            return MarshalInterface<IExtension>.FromAbi(pointer);
        }
        finally
        {
            Marshal.Release(pointer);
        }
    }

    private const uint ClsctxLocalServer = 0x4;
    private static readonly Guid IUnknown = new("00000000-0000-0000-C000-000000000046");

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(
        in Guid clsid,
        IntPtr outer,
        uint context,
        in Guid iid,
        out IntPtr pointer
    );
}
