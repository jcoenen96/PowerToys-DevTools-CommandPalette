using DevTools.Domain.Projects;
using DevTools.Infrastructure;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DevTools.CommandPalette.Commands;

/// <summary>Opens a solution or folder in its tool.</summary>
internal sealed partial class LaunchCommand : InvokableCommand
{
    private readonly DevToolsServices _services;
    private readonly LaunchTarget _target;

    public LaunchCommand(DevToolsServices services, LaunchTarget target)
    {
        _services = services;
        _target = target;
        Name = $"Open in {target.Tool.DisplayName}";
        Icon = Icons.For(target.Tool, services.Resolver);
    }

    public override ICommandResult Invoke()
    {
        var result = _services.Launcher.Launch(_target);
        if (result.Succeeded)
        {
            return CommandResult.Dismiss();
        }

        Toasts.Show(false, result.Message);
        return CommandResult.KeepOpen();
    }
}
