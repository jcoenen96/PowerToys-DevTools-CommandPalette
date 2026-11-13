namespace DevTools.Infrastructure.Repositories;

internal static class JsonFiles
{
    /// <summary>Reads a file while an editor may still hold it open.</summary>
    public static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Writes a file in one go: a crash halfway leaves the old file, not half a new one.</summary>
    public static void WriteAtomically(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, content);
        File.Move(temporary, path, overwrite: true);
    }
}
