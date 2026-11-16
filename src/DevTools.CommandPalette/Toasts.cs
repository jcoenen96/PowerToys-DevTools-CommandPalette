using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DevTools.CommandPalette;

internal static class Toasts
{
    public static void Show(bool succeeded, string message) =>
        new ToastStatusMessage(
            new StatusMessage { Message = message, State = succeeded ? MessageState.Success : MessageState.Error }
        )
        {
            Duration = succeeded ? 2500 : 6000,
        }.Show();
}
