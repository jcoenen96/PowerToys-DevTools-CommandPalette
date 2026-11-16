using DevTools.Application.Abstractions;
using DevTools.Domain.Git;
using DevTools.Domain.Tools;
using DevTools.Infrastructure.Tools;
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

    public static IconInfo Pin { get; } = new("\uE718");

    public static IconInfo Unpin { get; } = new("\uE77A");

    public static IconInfo Tag { get; } = new("\uE8EC");

    public static IconInfo Hide { get; } = new("\uED1A");

    public static IconInfo Show { get; } = new("\uE7B3");

    public static IconInfo Web { get; } = new("\uE774");

    public static IconInfo PullRequest { get; } = new("\uE8F1");

    public static IconInfo Search { get; } = new("\uE721");

    public static IconInfo Save { get; } = new("\uE74E");

    public static IconInfo Fetch { get; } = new("\uE896");

    public static IconInfo Run { get; } = new("\uE768");

    public static IconInfo For(GitRepository repository) => repository.IsWorktree ? Worktree : Repository;

    /// <summary>
    /// The tool's icon: a custom one from tools.json when set (a glyph, emoji, or image or program path), otherwise
    /// the icon of the program that launches it. For a launcher script or an app execution alias that is the
    /// program behind it (see <see cref="ExecutableIconLocator"/>), so PATH tools like <c>code.cmd</c> and
    /// <c>wt.exe</c> get their real icon too.
    /// </summary>
    public static IconInfo For(ToolDefinition tool, IExecutableResolver resolver)
    {
        if (!string.IsNullOrWhiteSpace(tool.Icon) && tool.Icon != ToolDefinition.DefaultIcon)
        {
            return new IconInfo(tool.Icon);
        }

        var program = ExecutableIconLocator.Find(resolver.Resolve(tool.Executables));
        return new IconInfo(program ?? ToolDefinition.DefaultIcon);
    }
}
