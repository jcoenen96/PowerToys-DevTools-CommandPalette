namespace DevTools.Application.Abstractions;

/// <summary>Turns a tool's executable candidates into the path of an installed executable.</summary>
public interface IExecutableResolver
{
    /// <returns>The first candidate that exists, or <see langword="null"/> when the tool is not installed.</returns>
    string? Resolve(IReadOnlyList<string> candidates);
}
