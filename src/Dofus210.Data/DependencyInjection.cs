using Dofus210.Data.Context;
using Dofus210.Data.Repositories;
using Dofus210.Data.UnitOfWork;
using Dofus210.Helper.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dofus210.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddDataAccess(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringNames.SqlServer);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = AppDataContextHardcode.SqlServerConnectionString;
        }

        services.AddDbContext<AppDataContext>(options =>
            options.UseSqlServer(
                connectionString,
                sqlServer => sqlServer.MigrationsAssembly(typeof(AppDataContext).Assembly.FullName)));

        services.AddScoped<IAppDataContextInitializer, AppDataContextInitializer>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork.UnitOfWork>();

        return services;
    }
}
