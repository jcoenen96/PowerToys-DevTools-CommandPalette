using DevTools.Application.Git;
using DevTools.CommandPalette.Commands;
using DevTools.Domain.Git;
using DevTools.Infrastructure;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DevTools.CommandPalette.Pages;

internal static partial class GitCommandFactory
{
    /// <summary>The command for a palette item: a page with the output, or a background run.</summary>
    public static ICommand For(DevToolsServices services, GitRepository repository, GitCommand command) =>
        command.ShowsOutput ? new GitOutputPage(services, repository, command) : Create(services, repository, command);

    /// <summary>A git command that runs in the background; destructive ones ask first.</summary>
    public static ICommand Create(DevToolsServices services, GitRepository repository, GitCommand command)
    {
        var run = new BackgroundCommand(
            command.Title,
            new IconInfo(command.Icon),
            $"{command.Description} in {repository.Name}…",
            async () =>
            {
                services.Preferences.RecordUse(repository.Path);
                var result = await services.Git.RunAsync(repository, command).ConfigureAwait(false);
                var summary = result.Summary;
                return (
                    result.Succeeded,
                    result.Succeeded
                        ? $"{command.Title} done in {repository.Name}{(summary.Length > 0 ? $": {FirstLine(summary)}" : "")}"
                        : $"{command.Title} failed in {repository.Name}: {summary}"
                );
            }
        );

        return command.RequiresConfirmation ? new ConfirmCommand(run, command, repository) : run;
    }

    private static string FirstLine(string text) => text.Split('\n')[0].Trim();

    private sealed partial class ConfirmCommand : InvokableCommand
    {
        private readonly ICommand _run;
        private readonly GitCommand _command;
        private readonly GitRepository _repository;

        public ConfirmCommand(ICommand run, GitCommand command, GitRepository repository)
        {
            _run = run;
            _command = command;
            _repository = repository;
            Name = command.Title;
            Icon = new IconInfo(command.Icon);
        }

        public override ICommandResult Invoke() =>
            CommandResult.Confirm(
                new ConfirmationArgs
                {
                    Title = $"{_command.Title} in {_repository.Name}?",
                    Description = $"This runs {_command.Description} in {_repository.Path} and can't be undone.",
                    PrimaryCommand = _run,
                    IsPrimaryCommandCritical = true,
                }
            );
    }
}
