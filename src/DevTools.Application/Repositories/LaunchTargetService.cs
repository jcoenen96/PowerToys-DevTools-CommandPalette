using DevTools.Application.Abstractions;
using DevTools.Domain.Git;
using DevTools.Domain.Projects;
using DevTools.Domain.Tools;

namespace DevTools.Application.Repositories;

/// <summary>Works out what a tool can open in each repository: its solutions, or the repository folder.</summary>
public sealed class LaunchTargetService(RepositoryService repositories, IFileFinder files, ISettingsProvider settings)
{
    public IReadOnlyList<LaunchTarget> GetTargets(ToolDefinition tool, bool refresh = false)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return [.. repositories.GetRepositories(refresh).SelectMany(repository => GetTargets(tool, repository))];
    }

    public IReadOnlyList<LaunchTarget> GetTargets(ToolDefinition tool, GitRepository repository)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(repository);

        if (tool.TargetKind == ToolTargetKind.Folder || tool.FilePatterns.Count == 0)
        {
            return [FolderTarget(tool, repository)];
        }

        var found = files.Find(repository.Path, tool.FilePatterns, settings.Current.FileSearchDepth);
        if (found.Count == 0)
        {
            return tool.FallBackToFolder ? [FolderTarget(tool, repository)] : [];
        }

        // A repository with both Shop.slnx and Shop.sln shows only the first pattern's file (the .slnx).
        return
        [
            .. found
                .GroupBy(path =>
                    Path.Combine(Path.GetDirectoryName(path) ?? "", Path.GetFileNameWithoutExtension(path))
                )
                .Select(group => group.OrderBy(path => PatternRank(tool, path)).First())
                .OrderBy(path => Depth(repository.Path, path))
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(path => new LaunchTarget(tool, repository, path, IsFolder: false)),
        ];
    }

    private static LaunchTarget FolderTarget(ToolDefinition tool, GitRepository repository) =>
        new(tool, repository, repository.Path, IsFolder: true);

    private static int PatternRank(ToolDefinition tool, string path)
    {
        var extension = Path.GetExtension(path);
        for (var i = 0; i < tool.FilePatterns.Count; i++)
        {
            if (tool.FilePatterns[i].EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return int.MaxValue;
    }

    private static int Depth(string root, string path) =>
        Path.GetRelativePath(root, path)
            .Count(c => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar);
}
