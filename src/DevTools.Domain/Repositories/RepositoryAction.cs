namespace DevTools.Domain.Repositories;

/// <summary>What a custom repository action does.</summary>
public enum RepositoryActionKind
{
    /// <summary>Opens <see cref="RepositoryAction.Url"/> in the default browser.</summary>
    Url,

    /// <summary>Starts <see cref="RepositoryAction.Executable"/> with <see cref="RepositoryAction.Arguments"/>.</summary>
    Command,

    /// <summary>Copies <see cref="RepositoryAction.Text"/> to the clipboard.</summary>
    Copy,
}

/// <summary>
/// A custom entry in a repository's menu, from <c>repositories.json</c>. Text fields take the placeholders of
/// <see cref="RepositoryPlaceholders"/>.
/// </summary>
public sealed record RepositoryAction
{
    public required string Name { get; init; }

    public RepositoryActionKind Kind { get; init; }

    public string? Url { get; init; }

    public string? Executable { get; init; }

    public string? Arguments { get; init; }

    /// <summary>Where a command starts; the repository root by default.</summary>
    public string? WorkingDirectory { get; init; }

    public string? Text { get; init; }

    /// <summary>Optional icon: a Segoe Fluent Icons glyph, an emoji, or a path to an image.</summary>
    public string? Icon { get; init; }

    /// <summary>The repositories the action is offered for.</summary>
    public RepositoryCondition When { get; init; } = RepositoryCondition.Always;

    /// <summary>
    /// Repeats the action for every file matching this pattern in the repository (<c>*.Tests.csproj</c>), with
    /// <c>{file}</c> and <c>{fileName}</c> set to each file.
    /// </summary>
    public string? ForEachFile { get; init; }
}
