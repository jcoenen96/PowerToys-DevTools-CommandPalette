namespace DevTools.Infrastructure.FileSystem;

/// <summary>Folders never worth searching: version control, build output and dependencies.</summary>
internal static class IgnoredFolders
{
    private static readonly HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".vs",
        ".idea",
        ".vscode",
        "bin",
        "obj",
        "node_modules",
        "packages",
        "artifacts",
        "TestResults",
        "AppPackages",
        "$RECYCLE.BIN",
    };

    public static bool Contains(string folderName) => Names.Contains(folderName);

    public static EnumerationOptions Options { get; } =
        new()
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            AttributesToSkip = FileAttributes.System,
            ReturnSpecialDirectories = false,
        };
}
