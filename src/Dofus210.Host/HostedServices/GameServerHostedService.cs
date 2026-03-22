using Dofus210.Bll.Services;
using Dofus210.Data.Context;
using Dofus210.Host.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dofus210.Host.HostedServices;

public sealed class GameServerHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<GameServerHostedService> _logger;
    private readonly ServerOptions _serverOptions;

    public GameServerHostedService(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<ServerOptions> serverOptions,
        ILogger<GameServerHostedService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _serverOptions = serverOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDataContext>();
        var bootstrapService = scope.ServiceProvider.GetRequiredService<IServerBootstrapService>();

        try
        {
            var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync(stoppingToken)).ToList();

            if (_serverOptions.AutoMigrate && pendingMigrations.Count > 0)
            {
                _logger.LogInformation("Applying {Count} pending migrations.", pendingMigrations.Count);
                await dbContext.Database.MigrateAsync(stoppingToken);
            }

            var canConnect = await dbContext.Database.CanConnectAsync(stoppingToken);
            var snapshot = await bootstrapService.GetSnapshotAsync(stoppingToken);

            _logger.LogInformation(
                "Host ready. Name={Name} AuthPort={AuthPort} GamePort={GamePort} DbAvailable={DbAvailable} PersistedAccounts={PersistedAccounts} HardcodedSeedAccounts={HardcodedSeedAccounts}",
                _serverOptions.Name,
                _serverOptions.AuthPort,
                _serverOptions.GamePort,
                canConnect,
                snapshot.PersistedAccountCount,
                snapshot.HardcodedAccountSeedCount);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Host bootstrap failed.");
            throw;
        }

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (TaskCanceledException)
        {
        }
    }
}

