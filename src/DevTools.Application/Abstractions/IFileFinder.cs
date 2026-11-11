namespace DevTools.Application.Abstractions;

/// <summary>Finds files by name pattern below a folder, skipping build output and dependency folders.</summary>
public interface IFileFinder
{
    IReadOnlyList<string> Find(string directory, IReadOnlyList<string> patterns, int maxDepth);
}
