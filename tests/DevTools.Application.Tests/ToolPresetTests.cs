using DevTools.Application.Tools;
using DevTools.Domain.Tools;

namespace DevTools.Application.Tests;

public class ToolPresetTests
{
    [Fact]
    public void Visual_studio_is_looked_up_without_matching_ssms_or_the_build_tools()
    {
        var visualStudio = ToolPresets.All.Single(tool => tool.Id == "visualstudio");
        var vswhere = visualStudio.Executables[0];

        Assert.StartsWith("vswhere:", vswhere, StringComparison.Ordinal);
        Assert.DoesNotContain("-products *", vswhere, StringComparison.Ordinal);
        Assert.Contains("Microsoft.VisualStudio.Product.Community", vswhere, StringComparison.Ordinal);
        Assert.All(
            visualStudio.Executables.Skip(1),
            path => Assert.EndsWith("devenv.exe", path, StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void Preset_ids_are_unique_and_never_empty()
    {
        Assert.All(ToolPresets.All, tool => Assert.False(string.IsNullOrWhiteSpace(tool.Id)));
        Assert.Equal(
            ToolPresets.All.Count,
            ToolPresets.All.Select(tool => tool.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()
        );
    }

    [Fact]
    public void Every_preset_uses_the_icon_of_its_program()
    {
        Assert.All(ToolPresets.All, tool => Assert.Equal(ToolDefinition.DefaultIcon, tool.Icon));
    }
}
