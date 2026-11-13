namespace DevTools.Domain.Repositories;

/// <summary>A search kept for later, such as <c>tag:work has:changes</c>, under a name.</summary>
public sealed record SavedSearch(string Name, string Query);
