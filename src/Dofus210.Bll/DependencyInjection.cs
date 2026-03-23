using Dofus210.Bll.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Dofus210.Bll;

public static class DependencyInjection
{
    public static IServiceCollection AddBusinessLogic(this IServiceCollection services)
    {
        services.AddScoped<IAccountDirectoryService, AccountDirectoryService>();
        services.AddScoped<IServerBootstrapService, ServerBootstrapService>();
        return services;
    }
}
