using Dofus210.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dofus210.Data.Context;

public sealed class AppDataContext : DbContext
{
    public AppDataContext(DbContextOptions<AppDataContext> options)
        : base(options)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDataContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}

