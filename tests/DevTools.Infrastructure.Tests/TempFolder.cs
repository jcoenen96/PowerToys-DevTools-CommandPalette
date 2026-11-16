using System.Diagnostics;

namespace DevTools.Infrastructure.Tests;

/// <summary>A temporary folder with helpers to create real git repositories and worktrees in it.</summary>
internal sealed class TempFolder : IDisposable
{
    public TempFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "devtools-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    public string File(string relativePath, string content = "")
    {
        var path = Combine(relativePath.Split('/'));
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, content);
        return path;
    }

    /// <summary>Creates a repository with one commit on <c>main</c>.</summary>
    public string Repository(string relativePath)
    {
        var path = Combine(relativePath.Split('/'));
        Directory.CreateDirectory(path);
        Git(path, "init", "--initial-branch=main", "--quiet");
        System.IO.File.WriteAllText(System.IO.Path.Combine(path, "README.md"), "# test\n");
        Git(path, "add", ".");
        Git(path, "-c", "user.name=Test", "-c", "user.email=test@example.com", "commit", "--quiet", "-m", "init");
        return path;
    }

    public static string Git(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {error}");
        }

        return output.Trim();
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Leave it for the OS to clean up.
        }
        catch (UnauthorizedAccessException)
        {
            // Git marks its objects read-only on Windows.
        }
    }
}
