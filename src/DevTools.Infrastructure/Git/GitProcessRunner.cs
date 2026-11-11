using System.ComponentModel;
using DevTools.Application.Abstractions;
using DevTools.Infrastructure.Processes;

namespace DevTools.Infrastructure.Git;

/// <summary>Runs the git executable from the settings (git on PATH by default).</summary>
public sealed class GitProcessRunner(ISettingsProvider settings) : IGitRunner
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    // Never wait for a username on a console nobody sees; Git Credential Manager still shows its own window.
    private static readonly Dictionary<string, string> Environment = new() { ["GIT_TERMINAL_PROMPT"] = "0" };

    public async Task<GitResult> RunAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default
    )
    {
        var git = settings.Current.GitExecutable;
        if (string.IsNullOrWhiteSpace(git))
        {
            git = "git";
        }

        try
        {
            var (exitCode, output, error) = await ProcessRunner
                .RunAsync(
                    System.Environment.ExpandEnvironmentVariables(git),
                    arguments,
                    workingDirectory,
                    Timeout,
                    Environment,
                    cancellationToken
                )
                .ConfigureAwait(false);
            return new GitResult(exitCode, output, error);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            return new GitResult(-1, "", $"Could not run git ({git}): {exception.Message}");
        }
    }
}
