using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DevTools.CommandPalette.Commands;

internal sealed partial class RefreshCommand : InvokableCommand
{
    private readonly Action _refresh;

    public RefreshCommand(Action refresh)
    {
        _refresh = refresh;
        Name = "Refresh";
        Icon = Icons.Refresh;
    }

    public override ICommandResult Invoke()
    {
        _refresh();
        return CommandResult.KeepOpen();
    }
}
