using DevTools.Infrastructure.Tools;
using Microsoft.Extensions.Time.Testing;

namespace DevTools.Infrastructure.Tests;

public sealed class ExecutableResolverTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private readonly FakeTimeProvider _time = new();

    [Fact]
    public void The_first_candidate_that_exists_wins()
    {
        var rider = _temp.File("Programs/Rider/bin/rider64.exe");

        var resolved = new ExecutableResolver(_time).Resolve([_temp.Combine("missing.exe"), rider]);

        Assert.Equal(rider, resolved);
    }

    [Fact]
    public void Wildcards_pick_the_newest_version()
    {
        _temp.File("JetBrains/JetBrains Rider 2025.9/bin/rider64.exe");
        var newest = _temp.File("JetBrains/JetBrains Rider 2025.10/bin/rider64.exe");
        _temp.File("JetBrains/JetBrains Rider 2024.3/bin/rider64.exe");

        var resolved = new ExecutableResolver(_time).Resolve([
            _temp.Combine("JetBrains", "JetBrains Rider*", "bin", "rider64.exe"),
        ]);

        Assert.Equal(newest, resolved);
    }

    [Fact]
    public void Environment_variables_are_expanded()
    {
        var code = _temp.File("Code/Code.exe");
        Environment.SetEnvironmentVariable("DEVTOOLS_TEST_ROOT", _temp.Path);

        var resolved = new ExecutableResolver(_time).Resolve([
            Path.Combine("%DEVTOOLS_TEST_ROOT%", "Code", "Code.exe"),
        ]);

        Assert.Equal(code, resolved);
    }

    [Fact]
    public void An_unknown_environment_variable_is_not_found() =>
        Assert.Null(new ExecutableResolver(_time).Resolve(["%DEVTOOLS_DOES_NOT_EXIST%/tool.exe"]));

    [Fact]
    public void Bare_names_are_looked_up_on_path()
    {
        // git is on PATH wherever these tests run.
        var resolved = new ExecutableResolver(_time).Resolve([OperatingSystem.IsWindows() ? "git.exe" : "git"]);

        Assert.NotNull(resolved);
        Assert.True(Path.IsPathRooted(resolved));
    }

    [Fact]
    public void Vswhere_candidates_use_the_paths_it_reports()
    {
        var devenv = _temp.File("VS/18/Enterprise/Common7/IDE/devenv.exe");
        string? arguments = null;
        var resolver = new ExecutableResolver(_time)
        {
            RunVsWhere = args =>
            {
                arguments = args;
                return $"{_temp.Combine("gone.exe")}\r\n{devenv}\r\n";
            },
        };

        Assert.Equal(devenv, resolver.Resolve(["vswhere: -latest -property productPath"]));
        Assert.Equal("-latest -property productPath", arguments);
    }

    [Fact]
    public void Results_are_cached_until_cleared()
    {
        var path = _temp.Combine("late.exe");
        var resolver = new ExecutableResolver(_time);
        Assert.Null(resolver.Resolve([path]));

        _temp.File("late.exe");
        Assert.Null(resolver.Resolve([path]));

        resolver.Clear();
        Assert.Equal(path, resolver.Resolve([path]));
    }

    public void Dispose() => _temp.Dispose();
}
