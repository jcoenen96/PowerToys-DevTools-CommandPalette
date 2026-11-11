using DevTools.Infrastructure;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DevTools.CommandPalette.Commands;

/// <summary>Opens a file or folder with its default app, e.g. a folder in File Explorer or tools.json in an editor.</summary>
internal sealed partial class OpenWithShellCommand : InvokableCommand
{
    private readonly DevToolsServices _services;
    private readonly string _path;
    private readonly Action? _before;

    public OpenWithShellCommand(
        DevToolsServices services,
        string path,
        string name,
        IconInfo icon,
        Action? before = null
    )
    {
        _services = services;
        _path = path;
        _before = before;
        Name = name;
        Icon = icon;
    }

    public override ICommandResult Invoke()
    {
        try
        {
            _before?.Invoke();
            _services.Shell.OpenWithShell(_path);
            return CommandResult.Dismiss();
        }
        catch (Exception exception)
            when (exception is IOException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        {
            Toasts.Show(false, $"Could not open {_path}: {exception.Message}");
            return CommandResult.KeepOpen();
        }
    }
}
