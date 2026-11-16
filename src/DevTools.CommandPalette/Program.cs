using Microsoft.CommandPalette.Extensions;
using Shmuelie.WinRTServer.CsWinRT;

namespace DevTools.CommandPalette;

public static class Program
{
    [MTAThread]
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "-RegisterProcessAsComServer")
        {
            global::Shmuelie.WinRTServer.ComServer server = new();
            using ManualResetEvent extensionDisposedEvent = new(false);

            // One extension instance, returned every time Command Palette asks for it; the process exits once
            // Command Palette disposes it.
            DevToolsExtension extensionInstance = new(extensionDisposedEvent);
            server.RegisterClass<DevToolsExtension, IExtension>(() => extensionInstance);
            server.Start();

            extensionDisposedEvent.WaitOne();
            server.Stop();
            server.UnsafeDispose();
        }
        else
        {
            Console.WriteLine("Not being launched as a Command Palette extension... exiting.");
        }
    }
}
