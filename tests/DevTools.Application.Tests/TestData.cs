using DevTools.Domain.Git;
using DevTools.Domain.Tools;

namespace DevTools.Application.Tests;

internal static class TestData
{
    public static string Root { get; } = Path.Combine(Path.GetTempPath(), "devtools-tests");

    public static GitRepository Repository(
        string name = "Shop",
        string branch = "main",
        RepositoryKind kind = RepositoryKind.Clone
    ) => new(name, Path.Combine(Root, name), new GitHead(branch, null), kind);

    public static ToolDefinition VisualStudio { get; } =
        new()
        {
            Id = "visualstudio",
            DisplayName = "Visual Studio",
            Executables = ["devenv.exe"],
            TargetKind = ToolTargetKind.Files,
            FilePatterns = ["*.slnx", "*.sln"],
            FallBackToFolder = true,
        };

    public static ToolDefinition Code { get; } =
        new()
        {
            Id = "vscode",
            DisplayName = "Visual Studio Code",
            Executables = ["code.cmd"],
        };
}
