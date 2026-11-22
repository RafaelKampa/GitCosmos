using GitCosmos.Domain.Entities;
using GitCosmos.Domain.Interfaces.Repositories;
using GitCosmos.Infrastructure.Data;

namespace GitCosmos.Infrastructure.Repositories;

public class Repository<T>(GitCosmosDbContext context) : IRepository<T> where T : BaseEntity
{
    protected readonly GitCosmosDbContext Context = context;

    public async Task<int> CreateAsync(T entity, CancellationToken cancellationToken = default)
    {
        Context.Set<T>().Add(entity);
        await Context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}

