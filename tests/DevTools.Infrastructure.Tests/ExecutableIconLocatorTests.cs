using System.Text;
using DevTools.Infrastructure.Tools;

namespace DevTools.Infrastructure.Tests;

public sealed class ExecutableIconLocatorTests : IDisposable
{
    private readonly TempFolder _temp = new();

    [Fact]
    public void A_program_is_its_own_icon()
    {
        var exe = _temp.File("Orca/Orca.exe");

        Assert.Equal(exe, ExecutableIconLocator.Find(exe));
    }

    [Fact]
    public void A_script_points_to_the_program_it_starts()
    {
        // VS Code's bin\code.cmd starts "%~dp0..\Code.exe".
        var code = _temp.File("Microsoft VS Code/Code.exe");
        var script = _temp.File(
            "Microsoft VS Code/bin/code.cmd",
            "@echo off\r\nset ELECTRON_RUN_AS_NODE=1\r\n\"%~dp0..\\Code.exe\" \"%~dp0..\\resources\\app\\out\\cli.js\" %*\r\n"
        );

        Assert.Equal(code, ExecutableIconLocator.Find(script));
    }

    [Fact]
    public void A_toolbox_script_points_to_the_ide_by_its_full_path()
    {
        var rider = _temp.File("Rider/bin/rider64.exe");
        var script = _temp.File("Toolbox/scripts/rider.cmd", $"@echo off\r\nstart \"\" \"{rider}\" %*\r\n");

        Assert.Equal(rider, ExecutableIconLocator.Find(script));
    }

    [Fact]
    public void A_script_without_a_path_uses_the_program_with_its_name_next_to_it()
    {
        var exe = _temp.File("tool/subl.exe");
        var script = _temp.File("tool/bin/subl.cmd", "@echo off\r\nrem nothing to see\r\n");

        Assert.Equal(exe, ExecutableIconLocator.Find(script));
    }

    [Fact]
    public void Nothing_to_show_for_a_missing_program()
    {
        Assert.Null(ExecutableIconLocator.Find(_temp.Combine("missing.exe")));
        Assert.Null(ExecutableIconLocator.Find(null));
    }

    [Fact]
    public void An_app_execution_alias_points_to_its_packaged_program()
    {
        const string Target = @"C:\Program Files\WindowsApps\Microsoft.WindowsTerminal_1.0_x64__8wekyb3d8bbwe\wt.exe";
        var strings = Encoding.Unicode.GetBytes(
            "Microsoft.WindowsTerminal_8wekyb3d8bbwe\0Microsoft.WindowsTerminal_8wekyb3d8bbwe!App\0"
                + Target
                + "\0"
                + "0\0"
        );
        var data = new List<byte>();
        data.AddRange(BitConverter.GetBytes(unchecked((int)0x8000001B)));
        data.AddRange(BitConverter.GetBytes((ushort)(strings.Length + 4)));
        data.AddRange(new byte[2]);
        data.AddRange(BitConverter.GetBytes(3));
        data.AddRange(strings);

        Assert.Equal(Target, ExecutableIconLocator.ParseAppExecLink(data.ToArray()));
        Assert.Null(ExecutableIconLocator.ParseAppExecLink(new byte[16]));
    }

    public void Dispose() => _temp.Dispose();
}
