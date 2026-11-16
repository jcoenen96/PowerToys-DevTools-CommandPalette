using System.Collections.Concurrent;
using DevTools.Application.Abstractions;
using DevTools.Infrastructure.Processes;

namespace DevTools.Infrastructure.Tools;

/// <summary>
/// Finds an installed executable from a tool's candidates: paths with environment variables and <c>*</c> wildcards
/// (the newest version wins), bare names on PATH, and <c>vswhere:</c> queries. Results are kept for a few minutes.
/// </summary>
public sealed class ExecutableResolver(TimeProvider time) : IExecutableResolver
{
    public const string VsWherePrefix = "vswhere:";

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private static readonly char[] Wildcards = ['*', '?'];

    private readonly ConcurrentDictionary<string, (string? Path, DateTimeOffset At)> _cache = new();

    /// <summary>Runs vswhere with the given arguments and returns its output; replaceable in tests.</summary>
    internal Func<string, string?> RunVsWhere { get; init; } = DefaultVsWhere;

    public string? Resolve(IReadOnlyList<string> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var key = string.Join('|', candidates);
        var now = time.GetUtcNow();
        if (_cache.TryGetValue(key, out var cached) && now - cached.At < CacheDuration)
        {
            return cached.Path;
        }

        var path = candidates.Select(ResolveCandidate).FirstOrDefault(found => found is not null);
        _cache[key] = (path, now);
        return path;
    }

    /// <summary>Forgets what was found, e.g. after the user changed tools.json.</summary>
    public void Clear() => _cache.Clear();

    private string? ResolveCandidate(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        candidate = candidate.Trim();
        if (candidate.StartsWith(VsWherePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return RunVsWhere(candidate[VsWherePrefix.Length..].Trim())
                ?.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault(File.Exists);
        }

        var expanded = Environment.ExpandEnvironmentVariables(candidate);
        if (expanded.Contains('%', StringComparison.Ordinal))
        {
            return null; // An environment variable this machine doesn't have.
        }

        if (!Path.IsPathRooted(expanded))
        {
            return FindOnPath(expanded);
        }

        return expanded.IndexOfAny(Wildcards) >= 0 ? ExpandWildcards(expanded)
            : File.Exists(expanded) ? expanded
            : null;
    }

    /// <summary>Expands the first wildcard segment, newest match first, and recurses into the rest of the path.</summary>
    internal static string? ExpandWildcards(string path)
    {
        var wildcard = path.IndexOfAny(Wildcards);
        if (wildcard < 0)
        {
            return File.Exists(path) ? path : null;
        }

        var separators = new[] { '\\', '/' };
        var segmentStart = path.LastIndexOfAny(separators, wildcard) + 1;
        var segmentEnd = path.IndexOfAny(separators, wildcard);
        var parent = path[..segmentStart];
        var pattern = segmentEnd < 0 ? path[segmentStart..] : path[segmentStart..segmentEnd];
        var rest = segmentEnd < 0 ? "" : path[(segmentEnd + 1)..];

        if (!Directory.Exists(parent))
        {
            return null;
        }

        try
        {
            var matches =
                rest.Length == 0
                    ? Directory.EnumerateFiles(parent, pattern)
                    : Directory.EnumerateDirectories(parent, pattern);

            return matches
                .OrderByDescending(Path.GetFileName, NaturalStringComparer.Instance)
                .Select(match => rest.Length == 0 ? match : ExpandWildcards(Path.Combine(match, rest)))
                .FirstOrDefault(found => found is not null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    internal static string? FindOnPath(string name)
    {
        var extensions =
            OperatingSystem.IsWindows() && !Path.HasExtension(name)
                ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD").Split(';')
                : [""];

        var folders = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(
            Path.PathSeparator,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );

        foreach (var folder in folders)
        {
            foreach (var extension in extensions)
            {
                var path = Path.Combine(Environment.ExpandEnvironmentVariables(folder), name + extension);
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }

        return null;
    }

    private static string? DefaultVsWhere(string arguments)
    {
        var vsWhere = Environment.ExpandEnvironmentVariables(
            "%ProgramFiles(x86)%\\Microsoft Visual Studio\\Installer\\vswhere.exe"
        );
        if (!File.Exists(vsWhere))
        {
            vsWhere = FindOnPath("vswhere.exe") ?? "";
        }

        if (vsWhere.Length == 0)
        {
            return null;
        }

        try
        {
            var (exitCode, output, _) = ProcessRunner
                .RunAsync(
                    vsWhere,
                    arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries),
                    null,
                    TimeSpan.FromSeconds(5)
                )
                .GetAwaiter()
                .GetResult();
            return exitCode == 0 ? output : null;
        }
        catch (Exception exception)
            when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }
}
