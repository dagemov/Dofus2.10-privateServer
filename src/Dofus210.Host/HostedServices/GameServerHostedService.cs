using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;
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
    private const short MaxMapCellId = 559;
    private const ushort CharactersListRequestModernMessageId = 2566;
    private const ushort CharacterCreationRequestModernMessageId = 1738;
    private const ushort CharacterSelectionModernMessageId = 6200;
    private static readonly byte[] FlashPolicyResponse = Encoding.ASCII.GetBytes(
        "<?xml version=\"1.0\"?><cross-domain-policy><allow-access-from domain=\"*\" to-ports=\"*\" /></cross-domain-policy>\0");

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
                    using var initialProbeCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    initialProbeCts.CancelAfter(TimeSpan.FromMilliseconds(250));

                    try
                    {
                        var initialBytesRead = await stream.ReadAsync(
                            buffer.AsMemory(0, _serverOptions.AuthReceiveBufferSize),
                            initialProbeCts.Token);

                        if (initialBytesRead > 0)
                        {
                            var initialChunk = buffer.AsSpan(0, initialBytesRead).ToArray();

                            if (ContainsPolicyFileRequest(ToSanitizedAscii(initialChunk)))
                            {
                                await RecordPacketAsync(connectionId, remoteEndPoint, "IN", initialChunk);
                                await stream.WriteAsync(FlashPolicyResponse, stoppingToken);
                                await RecordPacketAsync(connectionId, remoteEndPoint, "OUT", FlashPolicyResponse);

                                _logger.LogInformation(
                                    "Game flash policy response sent. ConnectionId={ConnectionId}",
                                    connectionId);

                                return;
                            }

                            await RecordPacketAsync(connectionId, remoteEndPoint, "IN", initialChunk);
                            packetBuffer.Append(initialChunk);
                        }
                    }
                    catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested && initialProbeCts.IsCancellationRequested)
                    {
                    }

                    await SendPayloadAsync(
                        stream,
                        connectionId,
                        remoteEndPoint,
                        LegacyDofus210Messages.CreateProtocolRequiredPacket(_serverOptions.AuthRequiredVersion),
                        stoppingToken);

                    await SendPayloadAsync(
                        stream,
                        connectionId,
                        remoteEndPoint,
                        LegacyDofus210Messages.CreateHelloGamePacket(),
                        stoppingToken);

                    await ProcessBufferedPacketsAsync(
                        packetBuffer,
                        state,
                        characterDirectoryService,
                        stream,
                        connectionId,
                        remoteEndPoint,
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
                        await ProcessBufferedPacketsAsync(
                            packetBuffer,
                            state,
                            characterDirectoryService,
                            stream,
                            connectionId,
                            remoteEndPoint,
                            stoppingToken);
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

    private async Task ProcessBufferedPacketsAsync(
        DofusPacketBuffer packetBuffer,
        GameConnectionState state,
        ICharacterDirectoryService characterDirectoryService,
        NetworkStream stream,
        string connectionId,
        string remoteEndPoint,
        CancellationToken stoppingToken)
    {
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
                    state.GameServerId = ticketSession.GameServerId;
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

            if (IsCharactersListRequestMessage(packet.MessageId))
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
                    ResolveGameServerId(state),
                    stoppingToken);

                _logger.LogInformation(
                    "CharactersListRequest received. ConnectionId={ConnectionId} AccountId={AccountId} CharacterCount={CharacterCount}",
                    connectionId,
                    state.Account.Id,
                    characters.Count);

                await SendCharactersListAsync(
                    stream,
                    connectionId,
                    remoteEndPoint,
                    state,
                    characters,
                    stoppingToken);

                continue;
            }

            if (await TryHandleCharacterCreationAsync(
                    packet,
                    state,
                    characterDirectoryService,
                    stream,
                    connectionId,
                    remoteEndPoint,
                    stoppingToken))
            {
                continue;
            }

            if (await TryHandleCharacterSelectionAsync(
                    packet,
                    state,
                    characterDirectoryService,
                    stream,
                    connectionId,
                    remoteEndPoint,
                    stoppingToken))
            {
                continue;
            }

            if (await TryHandleMapInformationsRequestAsync(
                    packet,
                    state,
                    stream,
                    connectionId,
                    remoteEndPoint,
                    stoppingToken))
            {
                continue;
            }

            if (await TryHandleGameContextReadyAsync(
                    packet,
                    state,
                    stream,
                    connectionId,
                    remoteEndPoint,
                    stoppingToken))
            {
                continue;
            }

            if (await TryHandleCharacterLoadingCompleteAsync(
                    packet,
                    state,
                    stream,
                    connectionId,
                    remoteEndPoint,
                    stoppingToken))
            {
                continue;
            }

            if (await TryHandleMapMovementRequestAsync(
                    packet,
                    state,
                    characterDirectoryService,
                    stream,
                    connectionId,
                    remoteEndPoint,
                    stoppingToken))
            {
                continue;
            }

            _logger.LogInformation(
                "Unhandled game message. ConnectionId={ConnectionId} MessageId={MessageId} Hex={Hex}",
                connectionId,
                packet.MessageId,
                Convert.ToHexString(packet.Payload));
        }
    }

    private async Task SendCharactersListAsync(
        NetworkStream stream,
        string connectionId,
        string remoteEndPoint,
        GameConnectionState state,
        IReadOnlyList<CharacterSummary> characters,
        CancellationToken cancellationToken)
    {
        state.SetKnownCharacters(characters);

        await SendPayloadAsync(
            stream,
            connectionId,
            remoteEndPoint,
            LegacyDofus210Messages.CreateCharactersListPacket(characters),
            cancellationToken);
    }

    private async Task<bool> TryHandleCharacterCreationAsync(
        DofusPacket packet,
        GameConnectionState state,
        ICharacterDirectoryService characterDirectoryService,
        NetworkStream stream,
        string connectionId,
        string remoteEndPoint,
        CancellationToken cancellationToken)
    {
        if (state.Account is null ||
            !TryResolveCharacterCreationRequest(packet, out var request) ||
            request is null)
        {
            return false;
        }

        var creationResult = await characterDirectoryService.CreateAsync(
            state.Account.Id,
            ResolveGameServerId(state),
            request,
            cancellationToken);

        _logger.LogInformation(
            "CharacterCreationRequest handled. ConnectionId={ConnectionId} MessageId={MessageId} AccountId={AccountId} Name={Name} BreedId={BreedId} ResultCode={ResultCode} Success={Success}",
            connectionId,
            packet.MessageId,
            state.Account.Id,
            request.Name,
            request.BreedId,
            creationResult.ResultCode,
            creationResult.Character is not null);

        await SendPayloadAsync(
            stream,
            connectionId,
            remoteEndPoint,
            LegacyDofus210Messages.CreateCharacterCreationResultPacket(creationResult.ResultCode),
            cancellationToken);

        if (creationResult.Character is null)
        {
            return true;
        }

        var characters = await characterDirectoryService.ListForAccountAsync(
            state.Account.Id,
            ResolveGameServerId(state),
            cancellationToken);

        await SendCharactersListAsync(
            stream,
            connectionId,
            remoteEndPoint,
            state,
            characters,
            cancellationToken);

        return true;
    }

    private async Task<bool> TryHandleCharacterSelectionAsync(
        DofusPacket packet,
        GameConnectionState state,
        ICharacterDirectoryService characterDirectoryService,
        NetworkStream stream,
        string connectionId,
        string remoteEndPoint,
        CancellationToken cancellationToken)
    {
        if (state.Account is null ||
            state.KnownCharacterIds.Count == 0 ||
            !TryResolveCharacterSelection(packet, state.KnownCharacterIds, out var characterId))
        {
            return false;
        }

        var selectionContext = await characterDirectoryService.GetSelectionContextAsync(
            state.Account.Id,
            ResolveGameServerId(state),
            characterId,
            cancellationToken);

        if (selectionContext is null)
        {
            _logger.LogWarning(
                "CharacterSelection rejected because the character context was not found. ConnectionId={ConnectionId} MessageId={MessageId} AccountId={AccountId} CharacterId={CharacterId}",
                connectionId,
                packet.MessageId,
                state.Account.Id,
                characterId);
            return false;
        }

        state.SelectedCharacterId = characterId;
        state.SelectedCharacter = selectionContext;

        _logger.LogInformation(
            "CharacterSelection received. ConnectionId={ConnectionId} MessageId={MessageId} AccountId={AccountId} CharacterId={CharacterId} MapId={MapId}",
            connectionId,
            packet.MessageId,
            state.Account.Id,
            characterId,
            selectionContext.MapId);

        var bootstrapPackets = new[]
        {
            LegacyDofus210Messages.CreateCharacterSelectedSuccessPacket(selectionContext),
            LegacyDofus210Messages.CreateGameContextCreatePacket(),
            LegacyDofus210Messages.CreateCharacterStatsListPacket(selectionContext),
            LegacyDofus210Messages.CreateCharacterCapabilitiesPacket(),
            LegacyDofus210Messages.CreateSetCharacterRestrictionsPacket(selectionContext),
            LegacyDofus210Messages.CreateInventoryContentPacket(selectionContext),
            LegacyDofus210Messages.CreateInventoryWeightPacket(),
            LegacyDofus210Messages.CreateSpellListPacket(),
            LegacyDofus210Messages.CreateShortcutBarContentPacket(0),
            LegacyDofus210Messages.CreateShortcutBarContentPacket(1),
            LegacyDofus210Messages.CreateEmoteListPacket(),
            LegacyDofus210Messages.CreateLifePointsRegenEndPacket(selectionContext),
            LegacyDofus210Messages.CreatePlayerLifeStatusPacket(),
            LegacyDofus210Messages.CreateCurrentMapPacket(selectionContext.MapId),
            LegacyDofus210Messages.CreateBasicDatePacket(DateTimeOffset.Now),
            LegacyDofus210Messages.CreateBasicTimePacket(DateTimeOffset.Now),
            LegacyDofus210Messages.CreateBasicNoOperationPacket()
        };

        foreach (var payload in bootstrapPackets)
        {
            await SendPayloadAsync(
                stream,
                connectionId,
                remoteEndPoint,
                payload,
                cancellationToken);
        }

        return true;
    }

    private async Task<bool> TryHandleMapInformationsRequestAsync(
        DofusPacket packet,
        GameConnectionState state,
        NetworkStream stream,
        string connectionId,
        string remoteEndPoint,
        CancellationToken cancellationToken)
    {
        if (state.SelectedCharacter is null ||
            packet.MessageId != DofusMessageIds.MapInformationsRequest)
        {
            return false;
        }

        if (packet.Payload.Length != 0 &&
            LegacyDofus210Messages.TryReadMapInformationsRequest(packet.Payload, out var requestedMapId) &&
            requestedMapId > 0 &&
            requestedMapId != state.SelectedCharacter.MapId)
        {
            _logger.LogWarning(
                "MapInformationsRequest received for a different map. ConnectionId={ConnectionId} RequestedMapId={RequestedMapId} SelectedMapId={SelectedMapId}",
                connectionId,
                requestedMapId,
                state.SelectedCharacter.MapId);
        }

        _logger.LogInformation(
            "MapInformationsRequest received. ConnectionId={ConnectionId} CharacterId={CharacterId} MapId={MapId}",
            connectionId,
            state.SelectedCharacter.Character.Id,
            state.SelectedCharacter.MapId);

        var mapPackets = new[]
        {
            LegacyDofus210Messages.CreateMapComplementaryInformationsDataPacket(
                state.SelectedCharacter,
                state.Account?.Id ?? 0),
            LegacyDofus210Messages.CreateMapFightCountPacket(),
            LegacyDofus210Messages.CreateBasicNoOperationPacket()
        };

        foreach (var payload in mapPackets)
        {
            await SendPayloadAsync(
                stream,
                connectionId,
                remoteEndPoint,
                payload,
                cancellationToken);
        }

        return true;
    }

    private async Task<bool> TryHandleGameContextReadyAsync(
        DofusPacket packet,
        GameConnectionState state,
        NetworkStream stream,
        string connectionId,
        string remoteEndPoint,
        CancellationToken cancellationToken)
    {
        if (state.SelectedCharacter is null ||
            !TryResolveGameContextReady(packet, state.SelectedCharacter.MapId, out var mapId))
        {
            return false;
        }

        state.IsWorldContextReady = true;

        _logger.LogInformation(
            "GameContextReady received. ConnectionId={ConnectionId} CharacterId={CharacterId} MapId={MapId}",
            connectionId,
            state.SelectedCharacter.Character.Id,
            mapId);

        await SendPayloadAsync(
            stream,
            connectionId,
            remoteEndPoint,
            LegacyDofus210Messages.CreateBasicNoOperationPacket(),
            cancellationToken);

        return true;
    }

    private async Task<bool> TryHandleCharacterLoadingCompleteAsync(
        DofusPacket packet,
        GameConnectionState state,
        NetworkStream stream,
        string connectionId,
        string remoteEndPoint,
        CancellationToken cancellationToken)
    {
        if (state.SelectedCharacter is null ||
            !IsCharacterLoadingCompleteMessage(packet.MessageId, packet.Payload.Length))
        {
            return false;
        }

        state.IsCharacterLoadingCompleted = true;

        _logger.LogInformation(
            "CharacterLoadingComplete received. ConnectionId={ConnectionId} CharacterId={CharacterId}",
            connectionId,
            state.SelectedCharacter.Character.Id);

        await SendPayloadAsync(
            stream,
            connectionId,
            remoteEndPoint,
            LegacyDofus210Messages.CreateBasicNoOperationPacket(),
            cancellationToken);

        return true;
    }

    private async Task<bool> TryHandleMapMovementRequestAsync(
        DofusPacket packet,
        GameConnectionState state,
        ICharacterDirectoryService characterDirectoryService,
        NetworkStream stream,
        string connectionId,
        string remoteEndPoint,
        CancellationToken cancellationToken)
    {
        if (state.Account is null ||
            state.SelectedCharacter is null ||
            !TryResolveGameMapMovementRequest(packet, state.SelectedCharacter.MapId, out var request) ||
            request is null)
        {
            return false;
        }

        var lastKeyMovement = request.KeyMovements[^1];
        var destinationCellId = LegacyDofus210Messages.DecodeCellId(lastKeyMovement);
        var direction = LegacyDofus210Messages.DecodeDirection(lastKeyMovement, state.SelectedCharacter.Direction);

        if (destinationCellId is < 0 or > MaxMapCellId)
        {
            _logger.LogWarning(
                "GameMapMovementRequest rejected because the destination cell is invalid. ConnectionId={ConnectionId} MessageId={MessageId} CharacterId={CharacterId} DestinationCellId={DestinationCellId}",
                connectionId,
                packet.MessageId,
                state.SelectedCharacter.Character.Id,
                destinationCellId);
            return true;
        }

        var updatedContext = await characterDirectoryService.UpdatePositionAsync(
            state.Account.Id,
            ResolveGameServerId(state),
            state.SelectedCharacter.Character.Id,
            state.SelectedCharacter.MapId,
            destinationCellId,
            direction,
            cancellationToken);

        if (updatedContext is null)
        {
            _logger.LogWarning(
                "GameMapMovementRequest could not persist the updated position. ConnectionId={ConnectionId} CharacterId={CharacterId}",
                connectionId,
                state.SelectedCharacter.Character.Id);
            return true;
        }

        state.SelectedCharacter = updatedContext;

        _logger.LogInformation(
            "GameMapMovementRequest handled. ConnectionId={ConnectionId} MessageId={MessageId} CharacterId={CharacterId} DestinationCellId={DestinationCellId} Direction={Direction} PathLength={PathLength}",
            connectionId,
            packet.MessageId,
            updatedContext.Character.Id,
            destinationCellId,
            direction,
            request.KeyMovements.Count);

        var refreshPackets = new[]
        {
            LegacyDofus210Messages.CreateMapComplementaryInformationsDataPacket(
                updatedContext,
                state.Account.Id),
            LegacyDofus210Messages.CreateMapFightCountPacket(),
            LegacyDofus210Messages.CreateBasicNoOperationPacket()
        };

        foreach (var payload in refreshPackets)
        {
            await SendPayloadAsync(
                stream,
                connectionId,
                remoteEndPoint,
                payload,
                cancellationToken);
        }

        return true;
    }

    private static bool TryResolveCharacterCreationRequest(
        DofusPacket packet,
        out CharacterCreationRequest? request)
    {
        request = null;

        if (packet.Payload.Length < 28 ||
            packet.Payload.Length > 256 ||
            !LegacyDofus210Messages.TryReadCharacterCreationRequest(packet.Payload, out request) ||
            request is null)
        {
            return false;
        }

        return packet.MessageId == CharacterCreationRequestModernMessageId ||
               packet.MessageId == DofusMessageIds.CharacterCreationRequest ||
               LooksLikeCharacterCreationRequest(request);
    }

    private static bool TryResolveCharacterSelection(
        DofusPacket packet,
        IReadOnlySet<long> knownCharacterIds,
        out long characterId)
    {
        characterId = 0;

        if (!LegacyDofus210Messages.TryReadCharacterSelection(packet.Payload, out characterId))
        {
            return false;
        }

        return packet.MessageId == CharacterSelectionModernMessageId ||
               packet.MessageId == DofusMessageIds.CharacterSelection ||
               knownCharacterIds.Contains(characterId);
    }

    private static bool IsCharactersListRequestMessage(ushort messageId)
    {
        return messageId == DofusMessageIds.CharactersListRequest ||
               messageId == CharactersListRequestModernMessageId;
    }

    private static bool TryResolveGameContextReady(
        DofusPacket packet,
        int selectedMapId,
        out long mapId)
    {
        mapId = 0;

        if (!LegacyDofus210Messages.TryReadGameContextReady(packet.Payload, out mapId))
        {
            return false;
        }

        return packet.MessageId == DofusMessageIds.GameContextReadyModern ||
               packet.MessageId == DofusMessageIds.GameContextReadyLegacyCandidate ||
               (packet.Payload.Length == sizeof(long) && mapId == selectedMapId);
    }

    private static bool TryResolveGameMapMovementRequest(
        DofusPacket packet,
        int selectedMapId,
        out LegacyGameMapMovementRequest? request)
    {
        request = null;

        if (!LegacyDofus210Messages.TryReadGameMapMovementRequest(packet.Payload, out request) ||
            request is null ||
            request.MapId != selectedMapId)
        {
            return false;
        }

        return packet.MessageId == DofusMessageIds.GameMapMovementRequestModern ||
               packet.MessageId == DofusMessageIds.GameMapMovementRequestLegacyCandidate ||
               packet.MessageId == DofusMessageIds.GameCautiousMapMovementRequestModern ||
               LooksLikeMapMovementRequest(request);
    }

    private static bool IsCharacterLoadingCompleteMessage(ushort messageId, int payloadLength)
    {
        return payloadLength == 0 &&
               (messageId == DofusMessageIds.CharacterLoadingCompleteModern ||
                messageId == DofusMessageIds.CharacterLoadingCompleteLegacyCandidate);
    }

    private static bool LooksLikeCharacterCreationRequest(CharacterCreationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            request.Name.Length is < 3 or > 20 ||
            request.BreedId is < 1 or > 20)
        {
            return false;
        }

        if (!char.IsLetter(request.Name[0]))
        {
            return false;
        }

        return request.Name.All(character => char.IsLetter(character) || character == '-');
    }

    private static bool LooksLikeMapMovementRequest(LegacyGameMapMovementRequest request)
    {
        if (request.KeyMovements.Count is 0 or > 64)
        {
            return false;
        }

        return request.KeyMovements.All(keyMovement =>
        {
            var cellId = LegacyDofus210Messages.DecodeCellId(keyMovement);
            return cellId is >= 0 and <= MaxMapCellId;
        });
    }

    private short ResolveGameServerId(GameConnectionState state)
    {
        return state.GameServerId ?? _serverOptions.GameServerId;
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

    private static bool ContainsPolicyFileRequest(string asciiPayload)
    {
        return asciiPayload.Contains("policy-file-request", StringComparison.OrdinalIgnoreCase);
    }

    private static string ToSanitizedAscii(byte[] payload)
    {
        return string.Create(payload.Length, payload, static (span, source) =>
        {
            for (var index = 0; index < source.Length; index++)
            {
                var currentByte = source[index];
                span[index] = currentByte is >= 32 and <= 126 ? (char)currentByte : '.';
            }
        });
    }

    private sealed class GameConnectionState
    {
        public AuthenticatedAccount? Account { get; set; }

        public short? GameServerId { get; set; }

        public long? SelectedCharacterId { get; set; }

        public CharacterSelectionContext? SelectedCharacter { get; set; }

        public bool IsWorldContextReady { get; set; }

        public bool IsCharacterLoadingCompleted { get; set; }

        public HashSet<long> KnownCharacterIds { get; } = [];

        public void SetKnownCharacters(IEnumerable<CharacterSummary> characters)
        {
            KnownCharacterIds.Clear();

            foreach (var character in characters)
            {
                KnownCharacterIds.Add(character.Id);
            }
        }
    }

}
