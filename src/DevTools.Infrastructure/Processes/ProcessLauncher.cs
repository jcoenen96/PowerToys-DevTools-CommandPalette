using System.Diagnostics;
using DevTools.Application.Abstractions;

namespace DevTools.Infrastructure.Processes;

public sealed class ProcessLauncher : IProcessLauncher
{
    public void Start(string executable, string arguments, string workingDirectory)
    {
        var isScript =
            executable.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
            || executable.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);

        // Scripts such as code.cmd or the JetBrains Toolbox launchers would flash a console window via the shell.
        var startInfo = new ProcessStartInfo(executable, arguments)
        {
            UseShellExecute = !isScript,
            CreateNoWindow = isScript,
            WorkingDirectory = Directory.Exists(workingDirectory) ? workingDirectory : "",
        };

        using var process = Process.Start(startInfo);
    }

    public void OpenWithShell(string path)
    {
        using var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}
