namespace DevTools.Domain.Tools;

/// <summary>What a tool opens.</summary>
public enum ToolTargetKind
{
    /// <summary>Files in a repository that match <see cref="ToolDefinition.FilePatterns"/>, such as solutions.</summary>
    Files,

    /// <summary>The repository or worktree folder itself.</summary>
    Folder,
}
