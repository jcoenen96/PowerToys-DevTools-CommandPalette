namespace DevTools.Domain.Repositories;

/// <summary>A tag such as <c>work</c>, given to every repository that matches <see cref="When"/>.</summary>
public sealed record TagRule(string Tag, RepositoryCondition When);
