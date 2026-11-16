using DevTools.Domain.Git;

namespace DevTools.Domain.Projects;

/// <summary>
/// One repository as the user thinks of it: the clone and the worktrees made from it, e.g. <c>Shop</c> with its
/// clone on <c>main</c> and a worktree on <c>feature/cart</c>. The clone always comes first.
/// </summary>
public sealed record Project(string Name, string Path, IReadOnlyList<GitRepository> Checkouts)
{
    /// <summary>
    /// Groups clones with their worktrees: a worktree belongs to the clone at its <see cref="GitRepository.MainRepositoryPath"/>.
    /// Each project lists its clone first, then its worktrees by branch; projects are sorted by name.
    /// </summary>
    public static IReadOnlyList<Project> Group(IEnumerable<GitRepository> repositories)
    {
        ArgumentNullException.ThrowIfNull(repositories);
        return
        [
            .. repositories
                .GroupBy(Key, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var checkouts = group
                        .OrderBy(repository => repository.IsWorktree)
                        .ThenBy(repository => repository.Head.DisplayName, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(repository => repository.Path, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    var main = checkouts[0];
                    return new Project(main.Name, main.IsWorktree ? Key(main) : main.Path, checkouts);
                })
                .OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(project => project.Path, StringComparer.OrdinalIgnoreCase),
        ];
    }

    private static string Key(GitRepository repository) =>
        Normalize(repository.IsWorktree ? repository.MainRepositoryPath ?? repository.Name : repository.Path);

    private static string Normalize(string path) =>
        System.IO.Path.TrimEndingDirectorySeparator(path.Replace('\\', '/'));
}
