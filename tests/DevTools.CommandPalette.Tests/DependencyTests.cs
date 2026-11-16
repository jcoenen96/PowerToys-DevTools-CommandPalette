using System.Reflection;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace DevTools.CommandPalette.Tests;

/// <summary>
/// The SDK's toolkit package doesn't declare all of its dependencies; a missing one crashes the extension the
/// first time it is needed (ToolGood.Words.Pinyin: the first search). Every assembly the toolkit references must
/// ship with the extension.
/// </summary>
public class DependencyTests
{
    [Fact]
    public void Every_assembly_the_toolkit_references_loads()
    {
        foreach (var reference in typeof(ListHelpers).Assembly.GetReferencedAssemblies())
        {
            var loaded = Assembly.Load(reference);
            Assert.NotNull(loaded);
        }
    }

    [Fact]
    public void Fuzzy_search_works()
    {
        Assert.True(FuzzyStringMatcher.ScoreFuzzy("shop", "Shop.slnx (main)") > 0);
    }
}
