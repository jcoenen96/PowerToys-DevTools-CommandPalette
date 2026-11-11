namespace DevTools.Domain.Tools;

/// <summary>The placeholders a tool's <see cref="ToolDefinition.Arguments"/> may contain.</summary>
public static class ArgumentPlaceholders
{
    /// <summary>The file or folder being opened.</summary>
    public const string Target = "{target}";

    /// <summary>The folder of the target (the target itself when it is a folder).</summary>
    public const string Folder = "{folder}";

    /// <summary>The root of the repository or worktree.</summary>
    public const string Repository = "{repo}";

    /// <summary>The checked out branch, or the short commit for a detached HEAD.</summary>
    public const string Branch = "{branch}";

    /// <summary>The file or folder name of the target.</summary>
    public const string Name = "{name}";

    public static IReadOnlyList<string> All { get; } = [Target, Folder, Repository, Branch, Name];
}
