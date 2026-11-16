namespace DevTools.Application.Git;

/// <summary>The short status of a repository, from <c>git status --porcelain=v2 --branch</c>.</summary>
public sealed record GitStatus(string? Upstream, int Ahead, int Behind, int Changed, int Untracked)
{
    public bool IsClean => Changed == 0 && Untracked == 0;

    /// <summary>Like <c>↑2 ↓1 · 3 changed</c>, or <c>clean</c>.</summary>
    public string Summary
    {
        get
        {
            var parts = new List<string>();
            if (Ahead > 0)
            {
                parts.Add($"↑{Ahead}");
            }

            if (Behind > 0)
            {
                parts.Add($"↓{Behind}");
            }

            if (Changed > 0)
            {
                parts.Add($"{Changed} changed");
            }

            if (Untracked > 0)
            {
                parts.Add($"{Untracked} untracked");
            }

            return parts.Count == 0 ? "clean" : string.Join(" · ", parts);
        }
    }

    public static GitStatus Parse(string porcelainV2)
    {
        ArgumentNullException.ThrowIfNull(porcelainV2);
        string? upstream = null;
        int ahead = 0,
            behind = 0,
            changed = 0,
            untracked = 0;

        foreach (
            var line in porcelainV2.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        )
        {
            if (line.StartsWith("# branch.upstream ", StringComparison.Ordinal))
            {
                upstream = line["# branch.upstream ".Length..];
            }
            else if (line.StartsWith("# branch.ab ", StringComparison.Ordinal))
            {
                var parts = line["# branch.ab ".Length..].Split(' ');
                ahead = int.Parse(parts[0].TrimStart('+'), System.Globalization.CultureInfo.InvariantCulture);
                behind = int.Parse(parts[1].TrimStart('-'), System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (line.StartsWith('?'))
            {
                untracked++;
            }
            else if (line[0] is '1' or '2' or 'u')
            {
                changed++;
            }
        }

        return new GitStatus(upstream, ahead, behind, changed, untracked);
    }
}
