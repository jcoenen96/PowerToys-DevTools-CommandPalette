namespace DevTools.Domain.Settings;

/// <summary>The user's settings, as the Command Palette settings page edits them.</summary>
public sealed record DevToolsSettings
{
    public const string DefaultWorktreePathTemplate = "{worktreeRoot}/{repo}/{branch}";

    /// <summary>Folders the repositories are cloned into.</summary>
    public IReadOnlyList<string> RepositoryRoots { get; init; } = [];

    /// <summary>
    /// Folders skipped while searching the repository and worktree folders, e.g. a worktree folder inside the
    /// repository folder. A full path skips that folder; a bare name skips every folder with that name.
    /// </summary>
    public IReadOnlyList<string> ExcludedFolders { get; init; } = [];

    /// <summary>Folders the worktrees are created in. New worktrees go into the first one.</summary>
    public IReadOnlyList<string> WorktreeRoots { get; init; } = [];

    /// <summary>How many folder levels below a root are searched for repositories.</summary>
    public int ScanDepth { get; init; } = 3;

    /// <summary>How many folder levels below a repository are searched for files such as solutions.</summary>
    public int FileSearchDepth { get; init; } = 3;

    /// <summary>Where <c>git worktree add</c> puts a new worktree; placeholders: worktreeRoot, repo, branch.</summary>
    public string WorktreePathTemplate { get; init; } = DefaultWorktreePathTemplate;

    /// <summary>
    /// Tool pages first list just the project names; choosing one lists its clone (first) and worktrees with their
    /// branch. Typing also finds those directly. Off lists every clone and worktree on the first page.
    /// </summary>
    public bool GroupByProject { get; init; } = true;

    /// <summary>The git executable; a bare name is looked up on PATH.</summary>
    public string GitExecutable { get; init; } = "git";

    /// <summary>Ids of tools the user switched off.</summary>
    public IReadOnlySet<string> DisabledToolIds { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
