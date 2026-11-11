using DevTools.Domain.Tools;

namespace DevTools.Application.Tools;

/// <summary>
/// The tools that work out of the box. Users switch them off on the settings page, or override one by adding a
/// tool with the same id to <c>tools.json</c>. Ids never change: Command Palette keys hotkeys on them.
/// </summary>
public static class ToolPresets
{
    private const string LocalPrograms = "%LOCALAPPDATA%\\Programs";
    private const string ToolboxScripts = "%LOCALAPPDATA%\\JetBrains\\Toolbox\\scripts";
    private const string ProgramFiles = "%ProgramFiles%";

    public static IReadOnlyList<ToolDefinition> All { get; } =
    [
        new()
        {
            Id = "visualstudio",
            DisplayName = "Visual Studio",
            // vswhere ships with every Visual Studio installer and finds the newest instance, previews included.
            Executables =
            [
                "vswhere:-latest -prerelease -products * -property productPath",
                $"{ProgramFiles}\\Microsoft Visual Studio\\*\\*\\Common7\\IDE\\devenv.exe",
            ],
            TargetKind = ToolTargetKind.Files,
            FilePatterns = ["*.slnx", "*.sln"],
            FallBackToFolder = true,
            IsPreset = true,
        },
        new()
        {
            Id = "vscode",
            DisplayName = "Visual Studio Code",
            Executables =
            [
                $"{LocalPrograms}\\Microsoft VS Code\\Code.exe",
                $"{ProgramFiles}\\Microsoft VS Code\\Code.exe",
                "code.cmd",
            ],
            TargetKind = ToolTargetKind.Folder,
            IsPreset = true,
        },
        new()
        {
            Id = "vscode-insiders",
            DisplayName = "Visual Studio Code - Insiders",
            Executables =
            [
                $"{LocalPrograms}\\Microsoft VS Code Insiders\\Code - Insiders.exe",
                $"{ProgramFiles}\\Microsoft VS Code Insiders\\Code - Insiders.exe",
                "code-insiders.cmd",
            ],
            TargetKind = ToolTargetKind.Folder,
            ShowAsTopLevelCommand = false,
            IsPreset = true,
        },
        JetBrains("rider", "Rider", ["Rider", "JetBrains Rider*"], "rider64.exe", "rider.cmd", solutions: true),
        JetBrains(
            "intellij-idea",
            "IntelliJ IDEA",
            ["IntelliJ IDEA*", "IntelliJ IDEA Ultimate", "IntelliJ IDEA Community Edition"],
            "idea64.exe",
            "idea.cmd"
        ),
        JetBrains("webstorm", "WebStorm", ["WebStorm*"], "webstorm64.exe", "webstorm.cmd"),
        JetBrains("pycharm", "PyCharm", ["PyCharm*"], "pycharm64.exe", "pycharm.cmd"),
        JetBrains("goland", "GoLand", ["GoLand*"], "goland64.exe", "goland.cmd"),
        JetBrains("clion", "CLion", ["CLion*"], "clion64.exe", "clion.cmd"),
        JetBrains("rustrover", "RustRover", ["RustRover*"], "rustrover64.exe", "rustrover.cmd"),
        JetBrains("datagrip", "DataGrip", ["DataGrip*"], "datagrip64.exe", "datagrip.cmd"),
        new()
        {
            // Orca, the desktop app for running coding agents in parallel worktrees (onorca.dev).
            Id = "orca",
            DisplayName = "Orca",
            Executables = [$"{LocalPrograms}\\Orca\\Orca.exe", $"{ProgramFiles}\\Orca\\Orca.exe", "orca.exe"],
            TargetKind = ToolTargetKind.Folder,
            IsPreset = true,
        },
        new()
        {
            Id = "terminal",
            DisplayName = "Windows Terminal",
            Executables = ["wt.exe"],
            Arguments = $"-d \"{ArgumentPlaceholders.Folder}\"",
            TargetKind = ToolTargetKind.Folder,
            ShowAsTopLevelCommand = false,
            IsPreset = true,
        },
        new()
        {
            Id = "explorer",
            DisplayName = "File Explorer",
            Executables = ["%WINDIR%\\explorer.exe"],
            TargetKind = ToolTargetKind.Folder,
            ShowAsTopLevelCommand = false,
            IsPreset = true,
        },
    ];

    /// <summary>
    /// A JetBrains IDE: installed by the Toolbox App (2.x installs into %LOCALAPPDATA%\Programs, older versions
    /// leave a script in Toolbox\scripts) or by the standalone installer (Program Files\JetBrains).
    /// </summary>
    private static ToolDefinition JetBrains(
        string id,
        string name,
        IReadOnlyList<string> folders,
        string executable,
        string toolboxScript,
        bool solutions = false
    ) =>
        new()
        {
            Id = id,
            DisplayName = name,
            Executables =
            [
                .. folders.Select(folder => $"{LocalPrograms}\\{folder}\\bin\\{executable}"),
                $"{ToolboxScripts}\\{toolboxScript}",
                .. folders.Select(folder => $"{ProgramFiles}\\JetBrains\\{folder}\\bin\\{executable}"),
                executable,
            ],
            TargetKind = solutions ? ToolTargetKind.Files : ToolTargetKind.Folder,
            FilePatterns = solutions ? ["*.slnx", "*.sln"] : [],
            FallBackToFolder = solutions,
            // The most common JetBrains IDEs show up top level; the rest stay reachable from every project's
            // "Open with" menu, and tools.json can promote them.
            ShowAsTopLevelCommand = id is "rider" or "intellij-idea" or "webstorm",
            IsPreset = true,
        };
}
