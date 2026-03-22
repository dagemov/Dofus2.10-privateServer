using Dofus210.Data.Repositories;

namespace Dofus210.Data.UnitOfWork;

public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
    IRepository<TEntity> Repository<TEntity>()
        where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

