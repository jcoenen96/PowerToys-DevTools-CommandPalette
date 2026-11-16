using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace DevTools.Infrastructure.Tools;

/// <summary>
/// Finds the program file whose icon stands for a tool. That is the resolved executable itself, unless it is a
/// launcher without an icon of its own:
/// <list type="bullet">
/// <item>a <c>.cmd</c>/<c>.bat</c> script such as VS Code's <c>bin\code.cmd</c> or a JetBrains Toolbox script: the
/// <c>.exe</c> it starts, or one next to it with the same name;</item>
/// <item>an app execution alias such as <c>%LOCALAPPDATA%\Microsoft\WindowsApps\wt.exe</c>: the packaged program it
/// points to.</item>
/// </list>
/// </summary>
public static partial class ExecutableIconLocator
{
    private const int AppExecLinkTag = unchecked((int)0x8000001B);

    private static readonly ConcurrentDictionary<string, string?> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <returns>The file to take the icon from, or <see langword="null"/> when there is none.</returns>
    public static string? Find(string? executable)
    {
        if (string.IsNullOrWhiteSpace(executable))
        {
            return null;
        }

        return Cache.GetOrAdd(executable, Locate);
    }

    /// <summary>Forgets what was found, e.g. after the user changed tools.json.</summary>
    public static void Clear() => Cache.Clear();

    private static string? Locate(string executable)
    {
        try
        {
            var extension = Path.GetExtension(executable);
            if (
                extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".bat", StringComparison.OrdinalIgnoreCase)
            )
            {
                return FromScript(executable, File.ReadAllText(executable));
            }

            if (OperatingSystem.IsWindows() && ReadAppExecLinkTarget(executable) is { } target)
            {
                return target;
            }

            return File.Exists(executable) ? executable : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The first existing <c>.exe</c> the script starts, else one next to (or above) it with its name.</summary>
    internal static string? FromScript(string script, string text)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(script)) ?? "";
        foreach (Match match in ExePath().Matches(text))
        {
            var path = match.Groups["quoted"].Success ? match.Groups["quoted"].Value : match.Groups["bare"].Value;
            path = path.Replace("%~dp0", folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            path = Environment.ExpandEnvironmentVariables(path);
            if (Path.DirectorySeparatorChar != '\\')
            {
                path = path.Replace('\\', Path.DirectorySeparatorChar);
            }

            if (path.Contains('%', StringComparison.Ordinal))
            {
                continue;
            }

            var full = Path.IsPathRooted(path) ? Path.GetFullPath(path) : Path.GetFullPath(Path.Combine(folder, path));
            if (File.Exists(full))
            {
                return full;
            }
        }

        // bin\code.cmd next to ..\Code.exe, and the like: the script's folder, then its parent.
        var name = Path.GetFileNameWithoutExtension(script) + ".exe";
        foreach (var candidate in new[] { folder, Path.GetDirectoryName(folder) })
        {
            if (string.IsNullOrEmpty(candidate) || !Directory.Exists(candidate))
            {
                continue;
            }

            var exe = Directory
                .EnumerateFiles(candidate, "*.exe")
                .FirstOrDefault(file => Path.GetFileName(file).Equals(name, StringComparison.OrdinalIgnoreCase));
            if (exe is not null)
            {
                return exe;
            }
        }

        return null;
    }

    /// <summary>
    /// The target of an app execution alias, from its reparse data: a version (3), then the package family name, the
    /// app id and the target path as null-terminated UTF-16 strings.
    /// </summary>
    internal static string? ParseAppExecLink(ReadOnlySpan<byte> reparseData)
    {
        // REPARSE_DATA_BUFFER: tag (4), data length (2), reserved (2), then the link: version (4) and the strings.
        const int Header = 8;
        if (reparseData.Length < Header + 4 || BitConverter.ToInt32(reparseData) != AppExecLinkTag)
        {
            return null;
        }

        var strings = Encoding.Unicode.GetString(reparseData[(Header + 4)..]).Split('\0');
        return strings.Length >= 3 && strings[2].Length > 0 ? strings[2] : null;
    }

    private static string? ReadAppExecLinkTarget(string path)
    {
        if (!File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
        {
            return null;
        }

        using var handle = CreateFileW(path, 0, 7, IntPtr.Zero, 3, 0x00200000 | 0x02000000, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            return null;
        }

        var buffer = new byte[16 * 1024];
        return DeviceIoControl(handle, 0x000900A8, IntPtr.Zero, 0, buffer, buffer.Length, out var read, IntPtr.Zero)
            ? ParseAppExecLink(buffer.AsSpan(0, read))
            : null;
    }

    [GeneratedRegex("\"(?<quoted>[^\"\\r\\n]+?\\.exe)\"|(?<bare>[^\\s\"]+?\\.exe)\\b", RegexOptions.IgnoreCase)]
    private static partial Regex ExePath();

    [LibraryImport(
        "kernel32.dll",
        EntryPoint = "CreateFileW",
        StringMarshalling = StringMarshalling.Utf16,
        SetLastError = true
    )]
    private static partial SafeFileHandle CreateFileW(
        string fileName,
        uint access,
        uint share,
        IntPtr security,
        uint creation,
        uint flags,
        IntPtr template
    );

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeviceIoControl(
        SafeFileHandle device,
        uint code,
        IntPtr input,
        int inputSize,
        [Out] byte[] output,
        int outputSize,
        out int returned,
        IntPtr overlapped
    );
}
