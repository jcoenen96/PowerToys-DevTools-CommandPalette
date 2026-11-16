using DevTools.Infrastructure.FileSystem;

namespace DevTools.Infrastructure.Tests;

public sealed class FileFinderTests : IDisposable
{
    private readonly TempFolder _temp = new();

    [Fact]
    public void Solutions_are_found_but_not_in_build_output_or_dependencies()
    {
        var root = _temp.File("Shop.slnx");
        var nested = _temp.File("tools/Tools.sln");
        _temp.File("bin/Debug/Copied.sln");
        _temp.File("node_modules/pkg/Some.sln");
        _temp.File("src/Shop/Shop.csproj");

        var found = new FileSystemFileFinder().Find(_temp.Path, ["*.slnx", "*.sln"], 3);

        Assert.Equal(new[] { nested, root }.Order(StringComparer.Ordinal), found.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void A_nested_repository_is_left_to_be_listed_on_its_own()
    {
        _temp.File("external/Lib/.git/HEAD", "ref: refs/heads/main");
        _temp.File("external/Lib/Lib.sln");

        Assert.Empty(new FileSystemFileFinder().Find(_temp.Path, ["*.sln"], 3));
    }

    public void Dispose() => _temp.Dispose();
}
