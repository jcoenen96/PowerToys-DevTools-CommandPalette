using DevTools.CommandPalette.Commands;
using DevTools.Domain.Git;
using DevTools.Infrastructure;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DevTools.CommandPalette.Pages;

/// <summary>
/// What every repository offers besides git and tools: its remotes on the web (browse, the branch, a new pull
/// request), the custom actions from repositories.json, and pinning or ignoring it.
/// </summary>
internal static class RepositoryExtras
{
    /// <summary>One entry of a repository's extras, for both the Ctrl+K menu and the git page.</summary>
    public sealed record Extra(ICommand Command, string Title, string Subtitle, IconInfo Icon, string Section)
    {
        public bool IsCritical { get; init; }
    }

    public static IEnumerable<Extra> RemoteItems(GitRepository repository)
    {
        var remotes = repository.Remotes.Where(remote => remote.WebUrl is not null).ToArray();
        foreach (var remote in remotes)
        {
            var label = remotes.Length == 1 ? "remote" : remote.Name;
            yield return new Extra(
                new OpenUrlCommand(remote.WebUrl!) { Name = "Browse" },
                remotes.Length == 1 ? "Browse repository" : $"Browse {remote.Name}",
                remote.WebUrl!,
                Icons.Web,
                "Remote"
            );

            if (repository.Head.Branch is { } branch)
            {
                if (RemoteUrls.BranchUrl(remote.WebUrl, branch) is { } branchUrl)
                {
                    yield return new Extra(
                        new OpenUrlCommand(branchUrl) { Name = "Browse branch" },
                        $"Browse {branch} on {label}",
                        branchUrl,
                        Icons.Branch,
                        "Remote"
                    );
                }

                if (RemoteUrls.PullRequestUrl(remote.WebUrl, branch) is { } pullRequestUrl)
                {
                    yield return new Extra(
                        new OpenUrlCommand(pullRequestUrl) { Name = "Create pull request" },
                        remotes.Length == 1
                            ? $"Create pull request for {branch}"
                            : $"Create pull request on {remote.Name}",
                        pullRequestUrl,
                        Icons.PullRequest,
                        "Remote"
                    );
                }
            }
        }

        foreach (var remote in repository.Remotes)
        {
            yield return new Extra(
                new CopyTextCommand(remote.Url) { Name = "Copy remote URL" },
                repository.Remotes.Count == 1 ? "Copy remote URL" : $"Copy {remote.Name} URL",
                remote.Url,
                Icons.Copy,
                "Remote"
            );
        }
    }

    public static IEnumerable<Extra> ActionItems(DevToolsServices services, GitRepository repository)
    {
        var entry = services.RepositoryCatalog.GetEntry(repository);
        foreach (var action in services.Actions.For(entry))
        {
            yield return new Extra(
                new RepositoryActionCommand(services, repository, action),
                action.Name,
                action.Kind == Domain.Repositories.RepositoryActionKind.Command
                    ? $"{action.Target} {action.Arguments}".Trim()
                    : action.Target,
                RepositoryActionCommand.IconFor(action),
                "Actions"
            );
        }
    }

    public static IEnumerable<Extra> ManageItems(DevToolsServices services, GitRepository repository, Action refresh)
    {
        var pin = new PinCommand(services, repository, refresh);
        yield return new Extra(
            pin,
            pin.Name,
            "Pinned repositories come first and match is:pinned",
            pin.Icon,
            "Repository"
        );
        yield return new Extra(
            new IgnoreCommand(services, repository, refresh),
            "Ignore this repository",
            "Hide it from every list; Git repositories > Ignored repositories shows it again",
            Icons.Hide,
            "Repository"
        )
        {
            IsCritical = true,
        };
    }

    public static IContextItem ToContextItem(Extra extra) =>
        new CommandContextItem(extra.Command)
        {
            Title = extra.Title,
            Subtitle = extra.Subtitle,
            Icon = extra.Icon,
            IsCritical = extra.IsCritical,
        };

    public static ListItem ToListItem(Extra extra) =>
        new(extra.Command)
        {
            Title = extra.Title,
            Subtitle = extra.Subtitle,
            Icon = extra.Icon,
            Section = extra.Section,
        };
}
