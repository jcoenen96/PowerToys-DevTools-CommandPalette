using DevTools.Domain.Repositories;

namespace DevTools.Application.Abstractions;

/// <summary>Keeps <see cref="RepositoryState"/> between sessions.</summary>
public interface IRepositoryStateStore
{
    RepositoryState Load();

    void Save(RepositoryState state);
}
