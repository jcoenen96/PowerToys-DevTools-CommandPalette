using DevTools.Application.Git;
using DevTools.Domain.Git;
using DevTools.Infrastructure;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DevTools.CommandPalette.Pages;

/// <summary>Shows the output of a read-only git command such as status or log.</summary>
internal sealed partial class GitOutputPage : ContentPage
{
    private readonly DevToolsServices _services;
    private readonly GitRepository _repository;
    private readonly GitCommand _command;

    public GitOutputPage(DevToolsServices services, GitRepository repository, GitCommand command)
    {
        _services = services;
        _repository = repository;
        _command = command;
        Name = command.Title;
        Title = $"{command.Description} in {repository.Name}";
        Icon = new IconInfo(command.Icon);
    }

    public override IContent[] GetContent()
    {
        IsLoading = true;
        try
        {
            var result = _services.Git.RunAsync(_repository, _command).GetAwaiter().GetResult();
            var text = string.IsNullOrWhiteSpace(result.Output) ? result.Error : result.Output;
            var fence = text.Contains("```", StringComparison.Ordinal) ? "~~~~" : "```";
            return
            [
                new MarkdownContent(
                    $"**{_repository.Name}** on `{_repository.Head.DisplayName}`\n\n{fence}text\n{text.TrimEnd()}\n{fence}"
                ),
            ];
        }
        finally
        {
            IsLoading = false;
        }
    }
}
