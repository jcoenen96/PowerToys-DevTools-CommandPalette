using System.Diagnostics;
using System.Text;

namespace DevTools.Infrastructure.Processes;

/// <summary>Runs a console program without a window and collects its output.</summary>
internal static class ProcessRunner
{
    public static async Task<(int ExitCode, string Output, string Error)> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        TimeSpan timeout,
        IReadOnlyDictionary<string, string>? environment = null,
        CancellationToken cancellationToken = default
    )
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        if (workingDirectory is not null)
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var (key, value) in environment ?? new Dictionary<string, string>())
        {
            startInfo.Environment[key] = value;
        }

        using var process =
            Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {executable}.");
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        var output = process.StandardOutput.ReadToEndAsync(timeoutSource.Token);
        var error = process.StandardError.ReadToEndAsync(timeoutSource.Token);

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
            return (process.ExitCode, await output.ConfigureAwait(false), await error.ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return (-1, "", $"{Path.GetFileName(executable)} did not finish within {timeout.TotalSeconds:0} seconds.");
        }
    }
}
