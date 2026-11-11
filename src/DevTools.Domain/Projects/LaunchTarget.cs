using DevTools.Domain.Git;
using DevTools.Domain.Tools;

namespace DevTools.Domain.Projects;

/// <summary>Something a tool can open: a solution or other file in a repository, or the repository folder.</summary>
public sealed record LaunchTarget(ToolDefinition Tool, GitRepository Repository, string Path, bool IsFolder)
{
    /// <summary>The file name (<c>Shop.slnx</c>) or, for a folder, the repository name.</summary>
    public string Name => IsFolder ? Repository.Name : System.IO.Path.GetFileName(Path);

    /// <summary>What the palette shows: the name with the branch behind it, e.g. <c>Shop.slnx (feature/cart)</c>.</summary>
    public string Title => $"{Name} ({Repository.Head.DisplayName})";

    public string Folder => IsFolder ? Path : System.IO.Path.GetDirectoryName(Path) ?? Repository.Path;
}
