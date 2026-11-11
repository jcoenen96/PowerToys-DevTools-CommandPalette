namespace DevTools.Domain.Git;

/// <summary>Rules for branch names, following <c>git check-ref-format --branch</c>.</summary>
public static class BranchNames
{
    private static readonly System.Buffers.SearchValues<char> ForbiddenCharacters = System.Buffers.SearchValues.Create(
        " ~^:?*[\\\t"
    );

    public static bool IsValid(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name == "@" || name.StartsWith('-'))
        {
            return false;
        }

        if (
            name.StartsWith('/')
            || name.EndsWith('/')
            || name.EndsWith('.')
            || name.Contains("..", StringComparison.Ordinal)
            || name.Contains("//", StringComparison.Ordinal)
            || name.Contains("@{", StringComparison.Ordinal)
            || name.AsSpan().ContainsAny(ForbiddenCharacters)
            || name.Any(char.IsControl)
        )
        {
            return false;
        }

        return name.Split('/').All(part => !part.StartsWith('.') && !part.EndsWith(".lock", StringComparison.Ordinal));
    }

    /// <summary>A folder name for a branch: <c>feature/login</c> becomes <c>feature-login</c>.</summary>
    public static string ToFolderName(string branch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branch);
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var chars = branch.Select(c => c is '/' or '\\' || invalid.Contains(c) ? '-' : c).ToArray();
        return new string(chars).Trim('-', '.');
    }
}
