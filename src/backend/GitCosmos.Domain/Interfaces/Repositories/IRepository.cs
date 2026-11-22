using GitCosmos.Domain.Entities;

namespace GitCosmos.Domain.Interfaces.Repositories;

public interface IRepository<T> where T : BaseEntity
{
    Task<int> CreateAsync(T entity, CancellationToken cancellationToken = default);
}

