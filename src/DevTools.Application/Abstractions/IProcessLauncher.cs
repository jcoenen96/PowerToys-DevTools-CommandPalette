namespace DevTools.Application.Abstractions;

public interface IProcessLauncher
{
    void Start(string executable, string arguments, string workingDirectory);

    /// <summary>Opens a file or folder with its default handler (Explorer for folders).</summary>
    void OpenWithShell(string path);
}
