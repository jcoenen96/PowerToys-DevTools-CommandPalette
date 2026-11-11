using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DevTools.CommandPalette.Commands;

/// <summary>
/// Runs slow work (git over the network, creating worktrees) in the background with a progress message, and shows
/// the outcome as a toast. The palette stays usable meanwhile.
/// </summary>
internal sealed partial class BackgroundCommand : InvokableCommand
{
    private readonly string _progress;
    private readonly Func<Task<(bool Succeeded, string Message)>> _work;
    private readonly Action<bool>? _completed;

    public BackgroundCommand(
        string name,
        IconInfo icon,
        string progress,
        Func<Task<(bool Succeeded, string Message)>> work,
        Action<bool>? completed = null
    )
    {
        Name = name;
        Icon = icon;
        _progress = progress;
        _work = work;
        _completed = completed;
    }

    /// <summary>What the palette does right after starting the work; going back suits a finished choice.</summary>
    public Func<CommandResult> Result { get; init; } = CommandResult.KeepOpen;

    public override ICommandResult Invoke()
    {
        _ = RunAsync();
        return Result();
    }

    private async Task RunAsync()
    {
        var status = new StatusMessage
        {
            Message = _progress,
            State = MessageState.Info,
            Progress = new ProgressState { IsIndeterminate = true },
        };
        ExtensionHost.ShowStatus(status, StatusContext.Extension);

        (bool Succeeded, string Message) outcome;
        try
        {
            outcome = await _work().ConfigureAwait(false);
        }
#pragma warning disable CA1031 // The work runs fire-and-forget; anything it throws must end up in front of the user.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            outcome = (false, exception.Message);
        }
        finally
        {
            ExtensionHost.HideStatus(status);
        }

        Toasts.Show(outcome.Succeeded, outcome.Message);
        _completed?.Invoke(outcome.Succeeded);
    }
}
