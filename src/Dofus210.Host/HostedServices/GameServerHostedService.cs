using System.Buffers;
using System.Net;
using System.Net.Sockets;
using Dofus210.Bll.Models;
using Dofus210.Bll.Services;
using Dofus210.Data.Context;
using Dofus210.Host.Auth;
using Dofus210.Host.Game;
using Dofus210.Host.Networking;
using Dofus210.Host.Options;
using Microsoft.Extensions.Options;

namespace Dofus210.Host.HostedServices;

public sealed class GameServerHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IAuthTicketStore _authTicketStore;
    private readonly IGameTrafficRecorder _gameTrafficRecorder;
    private readonly ILogger<GameServerHostedService> _logger;
    private readonly ServerOptions _serverOptions;
    private TcpListener? _listener;
    private long _connectionCounter;

    public GameServerHostedService(
        IServiceScopeFactory serviceScopeFactory,
        IHostEnvironment hostEnvironment,
        IAuthTicketStore authTicketStore,
        IGameTrafficRecorder gameTrafficRecorder,
        IOptions<ServerOptions> serverOptions,
        ILogger<GameServerHostedService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _hostEnvironment = hostEnvironment;
        _authTicketStore = authTicketStore;
        _gameTrafficRecorder = gameTrafficRecorder;
        _logger = logger;
        _serverOptions = serverOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var bootstrapScope = _serviceScopeFactory.CreateAsyncScope();

            var dbContext = bootstrapScope.ServiceProvider.GetRequiredService<AppDataContext>();
            var contextInitializer = bootstrapScope.ServiceProvider.GetRequiredService<IAppDataContextInitializer>();
            var bootstrapService = bootstrapScope.ServiceProvider.GetRequiredService<IServerBootstrapService>();
            var databaseCreated = await contextInitializer.EnsureCreatedAsync(stoppingToken);
            var canConnect = await dbContext.Database.CanConnectAsync(stoppingToken);
            var snapshot = await bootstrapService.GetSnapshotAsync(stoppingToken);

            _logger.LogInformation(
                "Host ready. Name={Name} AuthPort={AuthPort} GamePort={GamePort} DbAvailable={DbAvailable} DatabaseCreatedNow={DatabaseCreatedNow} PersistedAccounts={PersistedAccounts} HardcodedSeedAccounts={HardcodedSeedAccounts}",
                _serverOptions.Name,
                _serverOptions.AuthPort,
                _serverOptions.GamePort,
                canConnect,
                databaseCreated,
                snapshot.PersistedAccountCount,
                snapshot.HardcodedAccountSeedCount);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Host bootstrap failed.");
            throw;
        }

        var listenAddress = NetworkEndpointResolver.ResolveListenAddress(_serverOptions.Host);
        _listener = new TcpListener(listenAddress, _serverOptions.GamePort);
        _listener.Server.NoDelay = true;
        _listener.Start();

        _logger.LogInformation(
            "Game listener started on {Host}:{Port}.",
            listenAddress,
            _serverOptions.GamePort);

        _logger.LogInformation(
            "Game transcript path: {TranscriptPath}",
            ResolveTranscriptPath());

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var tcpClient = await _listener.AcceptTcpClientAsync(stoppingToken);
                _ = HandleClientAsync(tcpClient, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            _listener.Stop();
            _logger.LogInformation("Game listener stopped.");
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _listener?.Stop();
        return base.StopAsync(cancellationToken);
    }

    private async Task HandleClientAsync(TcpClient tcpClient, CancellationToken stoppingToken)
    {
        var connectionId = $"game-{Interlocked.Increment(ref _connectionCounter):D4}";
        var remoteEndPoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "unknown";
        var state = new GameConnectionState();
        var packetBuffer = new DofusPacketBuffer();
        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var characterDirectoryService = scope.ServiceProvider.GetRequiredService<ICharacterDirectoryService>();

        using (tcpClient)
        {
            tcpClient.NoDelay = true;

            _logger.LogInformation(
                "Game client connected. ConnectionId={ConnectionId} Remote={RemoteEndPoint}",
                connectionId,
                remoteEndPoint);

            try
            {
                using var stream = tcpClient.GetStream();
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                var buffer = ArrayPool<byte>.Shared.Rent(_serverOptions.AuthReceiveBufferSize);

                try
                {
                    await SendPayloadAsync(
                        stream,
                        connectionId,
                        remoteEndPoint,
                        LegacyDofus210Messages.CreateHelloGamePacket(),
                        stoppingToken);

                    while (!timeoutCts.IsCancellationRequested)
                    {
                        timeoutCts.CancelAfter(_serverOptions.GameReceiveTimeoutMs);

                        var bytesRead = await stream.ReadAsync(
                            buffer.AsMemory(0, _serverOptions.AuthReceiveBufferSize),
                            timeoutCts.Token);

                        if (bytesRead == 0)
                        {
                            _logger.LogInformation(
                                "Game client disconnected cleanly. ConnectionId={ConnectionId}",
                                connectionId);
                            break;
                        }

                        packetBuffer.Append(buffer.AsSpan(0, bytesRead));

                        while (packetBuffer.TryReadPacket(out var packetBytes))
                        {
                            await RecordPacketAsync(connectionId, remoteEndPoint, "IN", packetBytes);

                            if (!DofusPacketCodec.TryDecode(packetBytes, out var packet) || packet is null)
                            {
                                _logger.LogWarning(
                                    "Unable to decode framed game packet. ConnectionId={ConnectionId} Hex={Hex}",
                                    connectionId,
                                    Convert.ToHexString(packetBytes));
                                continue;
                            }

                            if (packet.MessageId == DofusMessageIds.AuthenticationTicket)
                            {
                                var authTicket = LegacyDofus210Messages.ReadAuthenticationTicket(packet.Payload);
                                var ticketAccepted = _authTicketStore.TryConsume(authTicket.Ticket, out var ticketSession);

                                if (!ticketAccepted)
                                {
                                    ticketAccepted = _authTicketStore.TryConsumeSingleOutstanding(out ticketSession);

                                    if (ticketAccepted)
                                    {
                                        _logger.LogWarning(
                                            "Game ticket fallback used. ConnectionId={ConnectionId} ProvidedTicket={Ticket}",
                                            connectionId,
                                            authTicket.Ticket);
                                    }
                                }

                                _logger.LogInformation(
                                    "AuthenticationTicket received. ConnectionId={ConnectionId} Lang={Lang} Ticket={Ticket} Accepted={Accepted}",
                                    connectionId,
                                    authTicket.Language,
                                    authTicket.Ticket,
                                    ticketAccepted);

                                if (ticketAccepted)
                                {
                                    state.Account = ticketSession.Account;
                                }

                                await SendPayloadAsync(
                                    stream,
                                    connectionId,
                                    remoteEndPoint,
                                    ticketAccepted
                                        ? LegacyDofus210Messages.CreateAuthenticationTicketAcceptedPacket()
                                        : LegacyDofus210Messages.CreateAuthenticationTicketRefusedPacket(),
                                    stoppingToken);

                                continue;
                            }

                            if (packet.MessageId == DofusMessageIds.CharactersListRequest)
                            {
                                if (state.Account is null)
                                {
                                    _logger.LogWarning(
                                        "CharactersListRequest received before game authentication. ConnectionId={ConnectionId}",
                                        connectionId);
                                    continue;
                                }

                                var characters = await characterDirectoryService.ListForAccountAsync(
                                    state.Account.Id,
                                    _serverOptions.GameServerId,
                                    stoppingToken);

                                _logger.LogInformation(
                                    "CharactersListRequest received. ConnectionId={ConnectionId} AccountId={AccountId} CharacterCount={CharacterCount}",
                                    connectionId,
                                    state.Account.Id,
                                    characters.Count);

                                await SendPayloadAsync(
                                    stream,
                                    connectionId,
                                    remoteEndPoint,
                                    LegacyDofus210Messages.CreateCharactersListPacket(characters),
                                    stoppingToken);

                                continue;
                            }

                            _logger.LogInformation(
                                "Unhandled game message. ConnectionId={ConnectionId} MessageId={MessageId} Hex={Hex}",
                                connectionId,
                                packet.MessageId,
                                Convert.ToHexString(packet.Payload));
                        }
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "Game client timed out waiting for more data. ConnectionId={ConnectionId} TimeoutMs={TimeoutMs}",
                    connectionId,
                    _serverOptions.GameReceiveTimeoutMs);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Game session failed. ConnectionId={ConnectionId}", connectionId);
            }
        }
    }

    private async Task SendPayloadAsync(
        NetworkStream stream,
        string connectionId,
        string remoteEndPoint,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        await stream.WriteAsync(payload, cancellationToken);
        await RecordPacketAsync(connectionId, remoteEndPoint, "OUT", payload);
    }

    private async Task RecordPacketAsync(
        string connectionId,
        string remoteEndPoint,
        string direction,
        byte[] payload)
    {
        var hexPayload = Convert.ToHexString(payload);

        _logger.LogInformation(
            "Game payload {Direction}. ConnectionId={ConnectionId} Remote={RemoteEndPoint} Bytes={Bytes} Hex={Hex}",
            direction,
            connectionId,
            remoteEndPoint,
            payload.Length,
            hexPayload);

        await _gameTrafficRecorder.RecordAsync(
            new GameTrafficRecord(
                DateTimeOffset.UtcNow,
                connectionId,
                direction,
                remoteEndPoint,
                payload.Length,
                hexPayload));
    }

    private string ResolveTranscriptPath()
    {
        if (Path.IsPathRooted(_serverOptions.GameTranscriptDirectory))
        {
            return Path.Combine(_serverOptions.GameTranscriptDirectory, "game-transcript.log");
        }

        return Path.GetFullPath(
            Path.Combine(
                _hostEnvironment.ContentRootPath,
                _serverOptions.GameTranscriptDirectory,
                "game-transcript.log"));
    }

    private sealed class GameConnectionState
    {
        public AuthenticatedAccount? Account { get; set; }
    }

}
