using Dofus210.Data.Context;
using Dofus210.Data.Repositories;
using System.Collections.Concurrent;

namespace Dofus210.Data.UnitOfWork;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDataContext _context;
    private readonly ConcurrentDictionary<Type, object> _repositories = new();

    public UnitOfWork(AppDataContext context)
    {
        _context = context;
    }

    public IRepository<TEntity> Repository<TEntity>()
        where TEntity : class
    {
        var repository = _repositories.GetOrAdd(
            typeof(TEntity),
            _ => new Repository<TEntity>(_context));

        return (IRepository<TEntity>)repository;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        return _context.DisposeAsync();
    }
}

