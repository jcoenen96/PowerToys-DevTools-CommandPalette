using DevTools.Application.Tools;
using DevTools.Domain.Tools;
using DevTools.Infrastructure.Tools;

namespace DevTools.Infrastructure.Tests;

public sealed class JsonToolCatalogTests : IDisposable
{
    private readonly TempFolder _temp = new();

    private string ConfigPath => _temp.Combine("tools.json");

    [Fact]
    public void Without_a_file_the_presets_are_the_tools()
    {
        using var catalog = new JsonToolCatalog(ConfigPath, watch: false);

        Assert.Equal(ToolPresets.All, catalog.GetTools());
        Assert.Null(catalog.ConfigurationError);
    }

    [Fact]
    public void The_example_file_is_valid_and_changes_nothing()
    {
        using var catalog = new JsonToolCatalog(ConfigPath, watch: false);
        catalog.EnsureConfigurationFile();

        Assert.Equal(ToolPresets.All, catalog.GetTools());
        Assert.Null(catalog.ConfigurationError);
    }

    [Fact]
    public void An_entry_with_a_preset_id_overrides_only_what_it_sets()
    {
        _temp.File(
            "tools.json",
            """
            {
              // comments and trailing commas are fine
              "tools": [
                { "id": "Rider", "executables": ["D:\\Rider\\bin\\rider64.exe"], },
              ],
            }
            """
        );
        using var catalog = new JsonToolCatalog(ConfigPath, watch: false);

        var rider = catalog.GetTools().Single(t => t.Id == "rider");

        Assert.Equal(["D:\\Rider\\bin\\rider64.exe"], rider.Executables);
        Assert.Equal("Rider", rider.DisplayName);
        Assert.Equal(["*.slnx", "*.sln"], rider.FilePatterns);
        Assert.True(rider.IsPreset);
    }

    [Fact]
    public void Other_entries_add_tools()
    {
        _temp.File(
            "tools.json",
            """
            { "tools": [ {
              "id": "sublime",
              "displayName": "Sublime Text",
              "executables": ["subl.exe"],
              "targetKind": "files",
              "filePatterns": ["*.sublime-project"],
              "arguments": "--project \"{target}\""
            } ] }
            """
        );
        using var catalog = new JsonToolCatalog(ConfigPath, watch: false);

        var sublime = catalog.GetTools()[^1];

        Assert.Equal(
            new ToolDefinition
            {
                Id = "sublime",
                DisplayName = "Sublime Text",
                Executables = sublime.Executables,
                TargetKind = ToolTargetKind.Files,
                FilePatterns = sublime.FilePatterns,
                Arguments = "--project \"{target}\"",
            },
            sublime
        );
        Assert.Equal(["*.sublime-project"], sublime.FilePatterns);
    }

    [Fact]
    public void A_broken_file_keeps_the_presets_and_says_why()
    {
        _temp.File("tools.json", "{ not json");
        using var catalog = new JsonToolCatalog(ConfigPath, watch: false);

        Assert.Equal(ToolPresets.All, catalog.GetTools());
        Assert.Contains("tools.json", catalog.ConfigurationError, StringComparison.Ordinal);
    }

    [Fact]
    public void A_new_tool_without_executables_is_skipped()
    {
        _temp.File("tools.json", """{ "tools": [ { "id": "nothing" } ] }""");
        using var catalog = new JsonToolCatalog(ConfigPath, watch: false);

        Assert.DoesNotContain(catalog.GetTools(), t => t.Id == "nothing");
        Assert.NotNull(catalog.ConfigurationError);
    }

    public void Dispose() => _temp.Dispose();
}
