using DevTools.Application.Abstractions;
using DevTools.Application.Tools;
using DevTools.Domain.Git;
using DevTools.Domain.Repositories;

namespace DevTools.Application.Repositories;

/// <summary>An action from <c>repositories.json</c> with its placeholders filled in for one repository.</summary>
public sealed record ResolvedRepositoryAction(
    string Name,
    RepositoryActionKind Kind,
    string Target,
    string Arguments,
    string WorkingDirectory,
    string? Icon
);

/// <summary>
/// The custom actions of a repository (open a URL, start a command, copy text, optionally once per matching file):
/// which apply, with their text filled in, and running them.
/// </summary>
public sealed class RepositoryActionService(
    IRepositoryConfiguration configuration,
    IFileFinder files,
    ISettingsProvider settings,
    IExecutableResolver resolver,
    IProcessLauncher launcher
)
{
    private const int MaxRepeats = 50;

    public IReadOnlyList<ResolvedRepositoryAction> For(RepositoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var result = new List<ResolvedRepositoryAction>();
        foreach (var action in configuration.Current.Actions)
        {
            if (
                string.IsNullOrWhiteSpace(action.Name)
                || !RepositoryConditions.Matches(action.When, entry.Repository, entry.Tags)
            )
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(action.ForEachFile))
            {
                if (Resolve(action, entry.Repository, file: null) is { } resolved)
                {
                    result.Add(resolved);
                }

                continue;
            }

            var found = files.Find(
                entry.Repository.Path,
                [action.ForEachFile.Trim()],
                settings.Current.FileSearchDepth
            );
            foreach (var file in found.Order(StringComparer.OrdinalIgnoreCase).Take(MaxRepeats))
            {
                if (Resolve(action, entry.Repository, file) is { } resolved)
                {
                    result.Add(resolved);
                }
            }
        }

        return result;
    }

    /// <summary>Starts a command action or opens a URL action; copying is up to the caller (the clipboard is UI).</summary>
    public LaunchResult Run(ResolvedRepositoryAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            switch (action.Kind)
            {
                case RepositoryActionKind.Url:
                    launcher.OpenWithShell(action.Target);
                    return LaunchResult.Success($"Opening {action.Target}");
                case RepositoryActionKind.Command:
                    var executable = resolver.Resolve([action.Target]) ?? action.Target;
                    launcher.Start(executable, action.Arguments, action.WorkingDirectory);
                    return LaunchResult.Success($"Started {action.Name}");
                default:
                    return LaunchResult.Failure($"{action.Name} is not something to run.");
            }
        }
        catch (Exception exception)
            when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            return LaunchResult.Failure($"Could not run {action.Name}: {exception.Message}");
        }
    }

    /// <summary>Fills in the placeholders of <see cref="RepositoryPlaceholders"/> and environment variables.</summary>
    public static string Render(string? template, GitRepository repository, string? file = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (string.IsNullOrEmpty(template))
        {
            return "";
        }

        var remote = repository.PrimaryRemote;
        var text = template
            .Replace(RepositoryPlaceholders.Path, repository.Path, StringComparison.OrdinalIgnoreCase)
            .Replace(RepositoryPlaceholders.Name, repository.Name, StringComparison.OrdinalIgnoreCase)
            .Replace(RepositoryPlaceholders.Branch, repository.Head.DisplayName, StringComparison.OrdinalIgnoreCase)
            .Replace(RepositoryPlaceholders.RemoteUrl, remote?.Url ?? "", StringComparison.OrdinalIgnoreCase)
            .Replace(RepositoryPlaceholders.WebUrl, remote?.WebUrl ?? "", StringComparison.OrdinalIgnoreCase)
            .Replace(
                RepositoryPlaceholders.FileName,
                file is null ? "" : Path.GetFileNameWithoutExtension(file),
                StringComparison.OrdinalIgnoreCase
            )
            .Replace(RepositoryPlaceholders.File, file ?? "", StringComparison.OrdinalIgnoreCase);
        return Environment.ExpandEnvironmentVariables(text);
    }

    private static ResolvedRepositoryAction? Resolve(RepositoryAction action, GitRepository repository, string? file)
    {
        var target = action.Kind switch
        {
            RepositoryActionKind.Url => Render(action.Url, repository, file),
            RepositoryActionKind.Command => Render(action.Executable, repository, file),
            _ => Render(action.Text, repository, file),
        };

        // A URL without a remote ("{webUrl}/wiki" in a local-only repository) or an empty command is left out.
        if (
            string.IsNullOrWhiteSpace(target)
            || (action.Kind == RepositoryActionKind.Url && !Uri.IsWellFormedUriString(target, UriKind.Absolute))
        )
        {
            return null;
        }

        var folder = string.IsNullOrWhiteSpace(action.WorkingDirectory)
            ? repository.Path
            : Render(action.WorkingDirectory, repository, file);
        return new ResolvedRepositoryAction(
            Render(action.Name, repository, file),
            action.Kind,
            target.Trim(),
            Render(action.Arguments, repository, file),
            folder,
            action.Icon
        );
    }
}
