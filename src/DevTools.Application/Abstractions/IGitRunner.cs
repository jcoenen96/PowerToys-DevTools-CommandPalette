namespace DevTools.Application.Abstractions;

/// <summary>Runs the git command line in a working folder.</summary>
public interface IGitRunner
{
    Task<GitResult> RunAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default
    );
}

public sealed record GitResult(int ExitCode, string Output, string Error)
{
    public bool Succeeded => ExitCode == 0;

    /// <summary>The most useful line to show the user: the error when git failed, otherwise the output.</summary>
    public string Summary
    {
        get
        {
            var text = Succeeded || string.IsNullOrWhiteSpace(Error) ? Output : Error;
            if (string.IsNullOrWhiteSpace(text))
            {
                text = Succeeded ? Error : Output;
            }

            return text.Trim();
        }
    }
}
