using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Dofus210.Data.Context;

public sealed class AppDataContextFactory : IDesignTimeDbContextFactory<AppDataContext>
{
    public AppDataContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDataContext>();

        optionsBuilder.UseSqlServer(
            AppDataContextHardcode.SqlServerConnectionString,
            sqlServer => sqlServer.MigrationsAssembly(typeof(AppDataContext).Assembly.FullName));

        return new AppDataContext(optionsBuilder.Options);
    }
}

