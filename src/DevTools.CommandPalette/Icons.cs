using DevTools.Application.Abstractions;
using DevTools.Domain.Git;
using DevTools.Domain.Tools;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DevTools.CommandPalette;

/// <summary>Segoe Fluent Icons glyphs and tool icons.</summary>
internal static class Icons
{
    public static IconInfo Extension { get; } = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");

    public static IconInfo Repository { get; } = new("\uE8B7");

    public static IconInfo Worktree { get; } = new("\uE8A4");

    public static IconInfo Branch { get; } = new("\uE8AB");

    public static IconInfo NewBranch { get; } = new("\uE710");

    public static IconInfo Folder { get; } = new("\uE838");

    public static IconInfo Copy { get; } = new("\uE8C8");

    public static IconInfo Refresh { get; } = new("\uE72C");

    public static IconInfo Settings { get; } = new("\uE713");

    public static IconInfo Edit { get; } = new("\uE70F");

    public static IconInfo Delete { get; } = new("\uE74D");

    public static IconInfo Info { get; } = new("\uE946");

    public static IconInfo Warning { get; } = new("\uE7BA");

    public static IconInfo For(GitRepository repository) => repository.IsWorktree ? Worktree : Repository;

    /// <summary>
    /// The tool's own icon from its executable when it is installed and the tool keeps the default glyph;
    /// otherwise the glyph, emoji or image path from the tool definition.
    /// </summary>
    public static IconInfo For(ToolDefinition tool, IExecutableResolver resolver)
    {
        if (tool.Icon == ToolDefinition.DefaultIcon || string.IsNullOrWhiteSpace(tool.Icon))
        {
            var executable = resolver.Resolve(tool.Executables);
            if (!string.IsNullOrEmpty(executable) && executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                return new IconInfo(executable);
            }
        }

        return new IconInfo(tool.Icon);
    }
}
