using System.Text;
using DevTools.Application.Git;
using DevTools.Application.Repositories;

namespace DevTools.Application.Search;

/// <summary>
/// A search with filters:
/// <list type="bullet">
/// <item><c>tag:work</c>, <c>name:shop*</c>, <c>branch:feature/*</c>, <c>path:work</c>, <c>remote:github.com</c></item>
/// <item><c>is:pinned</c>, <c>is:worktree</c>, <c>is:clone</c>, <c>is:detached</c>, <c>is:ahead</c>, <c>is:behind</c>,
/// <c>is:clean</c>, <c>has:changes</c>, <c>has:untracked</c>, <c>has:upstream</c>, <c>has:remote</c></item>
/// <item><c>-tag:archived</c> or <c>!tag:archived</c> excludes, <c>OR</c> (or <c>|</c>) between terms, and quotes
/// keep spaces: <c>path:"my repos"</c></item>
/// </list>
/// Filters all have to match (AND binds tighter than OR). What is left is <see cref="FreeText"/>, which the palette
/// matches fuzzily against names and branches, and exactly against tags.
/// </summary>
public sealed class RepositoryQuery
{
    private static readonly string[] Fields = ["tag", "name", "branch", "path", "remote", "is", "has"];

    /// <summary>The <c>is:</c> and <c>has:</c> values that need <c>git status</c>.</summary>
    private static readonly string[] StatusValues =
    [
        "ahead",
        "behind",
        "clean",
        "dirty",
        "changes",
        "change",
        "untracked",
        "upstream",
    ];

    private readonly IReadOnlyList<IReadOnlyList<Term>> _groups;

    private RepositoryQuery(IReadOnlyList<IReadOnlyList<Term>> groups, string freeText)
    {
        _groups = groups;
        FreeText = freeText;
    }

    public static RepositoryQuery Empty { get; } = new([], "");

    /// <summary>The words that are not filters, joined by spaces.</summary>
    public string FreeText { get; }

    /// <summary>Whether the query has any filter (anything but free text).</summary>
    public bool HasFilters => _groups.Any(group => group.Count > 0);

    /// <summary>Whether a filter needs <c>git status</c> (ahead, behind, changes).</summary>
    public bool NeedsStatus =>
        _groups
            .SelectMany(group => group)
            .Any(term =>
                term.Field is "is" or "has" && StatusValues.Contains(term.Value, StringComparer.OrdinalIgnoreCase)
            );

    public static RepositoryQuery Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Empty;
        }

        var groups = new List<List<Term>> { new() };
        var free = new List<string>();
        foreach (var token in Tokenize(text))
        {
            if (token is "OR" or "|" or "||")
            {
                if (groups[^1].Count > 0)
                {
                    groups.Add([]);
                }

                continue;
            }

            if (token is "AND" or "&&")
            {
                continue;
            }

            var negated = token.Length > 1 && token[0] is '-' or '!';
            var body = negated ? token[1..] : token;
            var colon = body.IndexOf(':', StringComparison.Ordinal);
            if (colon > 0 && colon < body.Length - 1)
            {
                var field = body[..colon].ToLowerInvariant();
                if (Fields.Contains(field))
                {
                    groups[^1].Add(new Term(field, body[(colon + 1)..].Trim('"'), negated));
                    continue;
                }
            }

            free.Add(token);
        }

        return new RepositoryQuery([.. groups.Where(g => g.Count > 0)], string.Join(' ', free));
    }

    /// <summary>Whether the filters match; free text is not checked here.</summary>
    /// <param name="entry">The repository with its tags and pin.</param>
    /// <param name="status">Its <c>git status</c>, when known; status filters don't match without it.</param>
    public bool Matches(RepositoryEntry entry, GitStatus? status)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return _groups.Count == 0 || _groups.Any(group => group.All(term => term.Matches(entry, status)));
    }

    /// <summary>Free-text words that are exactly one of the repository's tags, so <c>work</c> finds tag <c>work</c>.</summary>
    public bool FreeTextMatchesTag(RepositoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return FreeText.Length > 0
            && FreeText
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .All(word => entry.Tags.Contains(word, StringComparer.OrdinalIgnoreCase));
    }

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        foreach (var c in text)
        {
            if (c == '"')
            {
                quoted = !quoted;
                current.Append(c);
            }
            else if (char.IsWhiteSpace(c) && !quoted)
            {
                Flush();
            }
            else
            {
                current.Append(c);
            }
        }

        Flush();
        return tokens;

        void Flush()
        {
            if (current.Length > 0)
            {
                tokens.Add(current.ToString());
                current.Clear();
            }
        }
    }

    private sealed record Term(string Field, string Value, bool Negated)
    {
        public bool Matches(RepositoryEntry entry, GitStatus? status) => Test(entry, status) != Negated;

        private bool Test(RepositoryEntry entry, GitStatus? status)
        {
            var repository = entry.Repository;
            return Field switch
            {
                "tag" => entry.Tags.Any(tag => Wildcard.IsMatch(tag, Value)),
                "name" => Wildcard.IsMatch(
                    repository.Name,
                    Value.Contains('*', StringComparison.Ordinal) ? Value : $"*{Value}*"
                ),
                "branch" => Wildcard.IsMatch(
                    repository.Head.DisplayName,
                    Value.Contains('*', StringComparison.Ordinal) ? Value : $"{Value}*"
                ),
                "path" => repository
                    .Path.Replace('\\', '/')
                    .Contains(Value.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase),
                "remote" => repository.Remotes.Any(remote =>
                    remote.Url.Contains(Value, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(remote.Name, Value, StringComparison.OrdinalIgnoreCase)
                ),
                "is" => Value.ToLowerInvariant() switch
                {
                    "pinned" or "favorite" or "favourite" => entry.IsPinned,
                    "worktree" => repository.IsWorktree,
                    "clone" => !repository.IsWorktree,
                    "detached" => repository.Head.IsDetached,
                    "ahead" => status?.Ahead > 0,
                    "behind" => status?.Behind > 0,
                    "clean" => status?.IsClean == true,
                    "dirty" => status?.IsClean == false,
                    _ => false,
                },
                "has" => Value.ToLowerInvariant() switch
                {
                    "changes" or "change" => status is { Changed: > 0 } or { Untracked: > 0 },
                    "untracked" => status?.Untracked > 0,
                    "upstream" => status?.Upstream is not null,
                    "remote" or "remotes" => repository.Remotes.Count > 0,
                    _ => false,
                },
                _ => false,
            };
        }
    }
}
