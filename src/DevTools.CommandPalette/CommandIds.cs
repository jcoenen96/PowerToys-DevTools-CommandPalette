namespace DevTools.CommandPalette;

/// <summary>
/// Ids of the top-level commands. Command Palette stores hotkeys and aliases under these ids, so they must stay the
/// same between versions.
/// </summary>
internal static class CommandIds
{
    public const string Repositories = "devtools.repositories";
    public const string EditTools = "devtools.edit-tools";

    public static string Tool(string toolId) => $"devtools.tool.{toolId}";
}
