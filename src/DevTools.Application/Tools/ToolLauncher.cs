using DevTools.Application.Abstractions;
using DevTools.Domain.Projects;

namespace DevTools.Application.Tools;

/// <summary>Opens a launch target in its tool.</summary>
public sealed class ToolLauncher(IExecutableResolver resolver, IProcessLauncher launcher)
{
    public LaunchResult Launch(LaunchTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        var executable = resolver.Resolve(target.Tool.Executables);
        if (string.IsNullOrEmpty(executable))
        {
            return LaunchResult.Failure(
                $"{target.Tool.DisplayName} was not found. Set its executable in tools.json (Dev tools: edit tools)."
            );
        }

        try
        {
            launcher.Start(executable, ArgumentTemplate.Render(target.Tool.Arguments, target), target.Folder);
            return LaunchResult.Success($"Opening {target.Name} in {target.Tool.DisplayName}");
        }
        catch (Exception exception)
            when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return LaunchResult.Failure($"Could not start {target.Tool.DisplayName}: {exception.Message}");
        }
    }
}
