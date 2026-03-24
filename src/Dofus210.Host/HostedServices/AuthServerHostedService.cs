using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Dofus210.Bll.Models;
using Dofus210.Bll.Services;
using Dofus210.Host.Auth;
using Dofus210.Host.Networking;
using Dofus210.Host.Options;
using Microsoft.Extensions.Options;

namespace Dofus210.Host.HostedServices;

public sealed class AuthServerHostedService : BackgroundService
{
    private static readonly byte[] FlashPolicyResponse = Encoding.ASCII.GetBytes(
        "<?xml version=\"1.0\"?><cross-domain-policy><allow-access-from domain=\"*\" to-ports=\"*\" /></cross-domain-policy>\0");

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IAuthHandshakeFactory _authHandshakeFactory;
    private readonly IAuthTicketStore _authTicketStore;
    private readonly IAuthTrafficRecorder _trafficRecorder;
    private readonly ILogger<AuthServerHostedService> _logger;
    private readonly ServerOptions _serverOptions;
    private TcpListener? _listener;
    private long _connectionCounter;

    public AuthServerHostedService(
        IServiceScopeFactory serviceScopeFactory,
        IHostEnvironment hostEnvironment,
        IAuthHandshakeFactory authHandshakeFactory,
        IAuthTicketStore authTicketStore,
        IAuthTrafficRecorder trafficRecorder,
        IOptions<ServerOptions> serverOptions,
        ILogger<AuthServerHostedService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _hostEnvironment = hostEnvironment;
        _authHandshakeFactory = authHandshakeFactory;
        _authTicketStore = authTicketStore;
        _trafficRecorder = trafficRecorder;
        _logger = logger;
        _serverOptions = serverOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var listenAddress = NetworkEndpointResolver.ResolveListenAddress(_serverOptions.Host);

        _listener = new TcpListener(listenAddress, _serverOptions.AuthPort);
        _listener.Server.NoDelay = true;
        _listener.Start();

        _logger.LogInformation(
            "Auth listener started on {Host}:{Port}.",
            listenAddress,
            _serverOptions.AuthPort);

        _logger.LogInformation(
            "Auth transcript path: {TranscriptPath}",
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
            _logger.LogInformation("Auth listener stopped.");
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _listener?.Stop();
        return base.StopAsync(cancellationToken);
    }

    private async Task HandleClientAsync(TcpClient tcpClient, CancellationToken stoppingToken)
    {
        var connectionId = $"auth-{Interlocked.Increment(ref _connectionCounter):D4}";
        var remoteEndPoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "unknown";
        var state = new AuthConnectionState();
        var packetBuffer = new DofusPacketBuffer();
        using var handshakePayloads = _authHandshakeFactory.Create();

        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var accountDirectoryService = scope.ServiceProvider.GetRequiredService<IAccountDirectoryService>();
        var gameServerDirectoryService = scope.ServiceProvider.GetRequiredService<IGameServerDirectoryService>();

        using (tcpClient)
        {
            tcpClient.NoDelay = true;

            _logger.LogInformation(
                "Auth client connected. ConnectionId={ConnectionId} Remote={RemoteEndPoint}",
                connectionId,
                remoteEndPoint);

            try
            {
                using var stream = tcpClient.GetStream();
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                var buffer = ArrayPool<byte>.Shared.Rent(_serverOptions.AuthReceiveBufferSize);

                try
                {
                    while (!timeoutCts.IsCancellationRequested)
                    {
                        timeoutCts.CancelAfter(_serverOptions.AuthReceiveTimeoutMs);

                        var bytesRead = await stream.ReadAsync(
                            buffer.AsMemory(0, _serverOptions.AuthReceiveBufferSize),
                            timeoutCts.Token);

                        if (bytesRead == 0)
                        {
                            _logger.LogInformation(
                                "Auth client disconnected cleanly. ConnectionId={ConnectionId}",
                                connectionId);
                            break;
                        }

                        var chunk = buffer.AsSpan(0, bytesRead).ToArray();
                        var chunkAscii = ToSanitizedAscii(chunk);
                        var chunkKind = ClassifyPayload(chunk, chunkAscii);

                        if (ContainsPolicyFileRequest(chunkAscii))
                        {
                            await RecordIncomingPayloadAsync(connectionId, remoteEndPoint, chunk, stoppingToken);

                            await stream.WriteAsync(FlashPolicyResponse, stoppingToken);
                            await _trafficRecorder.RecordAsync(
                                new AuthTrafficRecord(
                                    DateTimeOffset.UtcNow,
                                    connectionId,
                                    "OUT",
                                    remoteEndPoint,
                                    FlashPolicyResponse.Length,
                                    ToHex(FlashPolicyResponse),
                                    ToSanitizedAscii(FlashPolicyResponse)),
                                stoppingToken);

                            _logger.LogInformation(
                                "Flash policy response sent. ConnectionId={ConnectionId}",
                                connectionId);
                            break;
                        }

                        if (chunkKind == "PlainTextCandidate")
                        {
                            await RecordIncomingPayloadAsync(connectionId, remoteEndPoint, chunk, stoppingToken);

                            if (TryExtractUsername(chunkAscii, out var username))
                            {
                                var account = await accountDirectoryService.FindByUsernameAsync(username, stoppingToken);

                                _logger.LogInformation(
                                    "Auth probe identified username candidate. ConnectionId={ConnectionId} Username={Username} KnownAccount={KnownAccount}",
                                    connectionId,
                                    username,
                                    account is not null);
                            }

                            continue;
                        }

                        packetBuffer.Append(chunk);

                        while (packetBuffer.TryReadPacket(out var packetBytes))
                        {
                            await RecordIncomingPayloadAsync(connectionId, remoteEndPoint, packetBytes, stoppingToken);

                            if (!DofusPacketCodec.TryDecode(packetBytes, out var packet) || packet is null)
                            {
                                _logger.LogWarning(
                                    "Unable to decode framed auth packet. ConnectionId={ConnectionId} Hex={Hex}",
                                    connectionId,
                                    ToHex(packetBytes));
                                continue;
                            }

                            await HandleDofusPacketAsync(
                                stream,
                                connectionId,
                                remoteEndPoint,
                                packet,
                                state,
                                handshakePayloads,
                                accountDirectoryService,
                                gameServerDirectoryService,
                                stoppingToken);

                            if (state.CloseAfterCurrentPacket)
                            {
                                _logger.LogInformation(
                                    "Auth session closing after server handoff. ConnectionId={ConnectionId}",
                                    connectionId);
                                return;
                            }
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
                    "Auth client timed out waiting for more data. ConnectionId={ConnectionId} TimeoutMs={TimeoutMs}",
                    connectionId,
                    _serverOptions.AuthReceiveTimeoutMs);
            }
            catch (IOException exception) when (IsExpectedRemoteDisconnect(exception))
            {
                _logger.LogInformation(
                    "Auth client closed the connection during session flow. ConnectionId={ConnectionId} Reason={Reason}",
                    connectionId,
                    ExtractSocketError(exception)?.ToString() ?? exception.GetType().Name);
            }
            catch (SocketException exception) when (IsExpectedRemoteDisconnect(exception))
            {
                _logger.LogInformation(
                    "Auth client closed the socket during session flow. ConnectionId={ConnectionId} Reason={Reason}",
                    connectionId,
                    exception.SocketErrorCode);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Auth session failed. ConnectionId={ConnectionId}", connectionId);
            }
        }
    }

    private async Task HandleDofusPacketAsync(
        NetworkStream stream,
        string connectionId,
        string remoteEndPoint,
        DofusPacket packet,
        AuthConnectionState state,
        AuthHandshakePayloads handshakePayloads,
        IAccountDirectoryService accountDirectoryService,
        IGameServerDirectoryService gameServerDirectoryService,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Dofus packet decoded. ConnectionId={ConnectionId} MessageId={MessageId} PayloadLength={PayloadLength}",
            connectionId,
            packet.MessageId,
            packet.PayloadLength);

        if (!state.BootstrapSent &&
            CapturedAuthBootstrapPackets.TryGet(_serverOptions.AuthBootstrapProfile, out var capturedPackets))
        {
            foreach (var capturedPacket in capturedPackets)
            {
                await SendPayloadAsync(
                    stream,
                    connectionId,
                    remoteEndPoint,
                    capturedPacket,
                    cancellationToken);
            }

            state.BootstrapSent = true;

            _logger.LogInformation(
                "Captured auth bootstrap sent. ConnectionId={ConnectionId} Profile={Profile} PacketCount={PacketCount}",
                connectionId,
                _serverOptions.AuthBootstrapProfile,
                capturedPackets.Count);

            return;
        }

        if (packet.MessageId == DofusMessageIds.BasicPing)
        {
            var pongPayload = packet.Payload.ToArray();
            var pongPacket = DofusPacketCodec.Encode(DofusMessageIds.BasicPong, pongPayload);

            await SendPayloadAsync(
                stream,
                connectionId,
                remoteEndPoint,
                pongPacket,
                cancellationToken);

            _logger.LogInformation(
                "BasicPong sent. ConnectionId={ConnectionId} QuietFlag={QuietFlag}",
                connectionId,
                pongPayload.Length > 0 && pongPayload[0] != 0);

            if (!state.BootstrapSent)
            {
                await SendPayloadAsync(
                    stream,
                    connectionId,
                    remoteEndPoint,
                    handshakePayloads.ProtocolRequiredPacket,
                    cancellationToken);

                await SendPayloadAsync(
                    stream,
                    connectionId,
                    remoteEndPoint,
                    handshakePayloads.HelloConnectPacket,
                    cancellationToken);

                state.BootstrapSent = true;

                _logger.LogInformation(
                    "Auth bootstrap sent. ConnectionId={ConnectionId} RequiredVersion={RequiredVersion} CurrentVersion={CurrentVersion} SaltLength={SaltLength} PublicKeyLength={PublicKeyLength}",
                    connectionId,
                    _serverOptions.AuthRequiredVersion,
                    _serverOptions.AuthCurrentVersion,
                    handshakePayloads.Salt.Length,
                    handshakePayloads.KeyBytes.Length);
            }

            return;
        }

        switch (packet.MessageId)
        {
            case DofusMessageIds.Identification:
                await HandleIdentificationAsync(
                    stream,
                    connectionId,
                    remoteEndPoint,
                    packet.Payload,
                    state,
                    accountDirectoryService,
                    gameServerDirectoryService,
                    cancellationToken);
                break;

            case DofusMessageIds.ClientKey:
                state.ClientKey = LegacyDofus210Messages.ReadClientKey(packet.Payload);
                _logger.LogInformation(
                    "Client key received. ConnectionId={ConnectionId} KeyLength={KeyLength}",
                    connectionId,
                    state.ClientKey.Length);
                break;

            case DofusMessageIds.ServerSelection:
                await HandleServerSelectionAsync(
                    stream,
                    connectionId,
                    remoteEndPoint,
                    packet.Payload,
                    state,
                    gameServerDirectoryService,
                    cancellationToken);
                break;

            default:
                _logger.LogInformation(
                    "Unhandled auth message. ConnectionId={ConnectionId} MessageId={MessageId} Hex={Hex}",
                    connectionId,
                    packet.MessageId,
                    ToHex(packet.Payload));
                break;
        }
    }

    private async Task HandleIdentificationAsync(
        NetworkStream stream,
        string connectionId,
        string remoteEndPoint,
        ReadOnlyMemory<byte> payload,
        AuthConnectionState state,
        IAccountDirectoryService accountDirectoryService,
        IGameServerDirectoryService gameServerDirectoryService,
        CancellationToken cancellationToken)
    {
        if (!LegacyDofus210Messages.TryReadIdentification(payload.Span, out var identification) ||
            identification is null)
        {
            _logger.LogWarning(
                "Legacy IdentificationMessage could not be decoded. ConnectionId={ConnectionId} Hex={Hex}",
                connectionId,
                ToHex(payload.Span));
            return;
        }

        if (!LegacyDofus210Messages.TryReadCredentials(identification.Credentials, out var credentials) ||
            credentials is null)
        {
            _logger.LogWarning(
                "Credential blob could not be decoded. ConnectionId={ConnectionId} CredentialsHex={Hex}",
                connectionId,
                ToHex(identification.Credentials));
            return;
        }

        _logger.LogInformation(
            "Identification received. ConnectionId={ConnectionId} Version={Major}.{Minor}.{Release}.{Revision}.{Patch} BuildType={BuildType} Lang={Lang} Username={Username} RequestedServerId={ServerId} AutoConnect={AutoConnect}",
            connectionId,
            identification.Version.Major,
            identification.Version.Minor,
            identification.Version.Release,
            identification.Version.Revision,
            identification.Version.Patch,
            identification.Version.BuildType,
            identification.Language,
            credentials.Username,
            identification.ServerId,
            identification.AutoConnect);

        var authentication = await accountDirectoryService.ValidateCredentialsDetailedAsync(
            credentials.Username,
            credentials.Password,
            cancellationToken);

        if (authentication.Account is null)
        {
            await SendPayloadAsync(
                stream,
                connectionId,
                remoteEndPoint,
                LegacyDofus210Messages.CreateIdentificationFailedPacket(1),
                cancellationToken);

            _logger.LogInformation(
                "Identification refused. ConnectionId={ConnectionId} Username={Username} UsernameExists={UsernameExists} PasswordMatched={PasswordMatched} PasswordLength={PasswordLength}",
                connectionId,
                credentials.Username,
                authentication.UsernameExists,
                authentication.PasswordMatched,
                credentials.Password.Length);

            return;
        }

        state.Account = authentication.Account;
        var availableServers = await gameServerDirectoryService.ListForAccountAsync(
            authentication.Account.Id,
            cancellationToken);

        await SendPayloadAsync(
            stream,
            connectionId,
            remoteEndPoint,
            LegacyDofus210Messages.CreateCredentialsAcknowledgementPacket(),
            cancellationToken);

        await SendPayloadAsync(
            stream,
            connectionId,
            remoteEndPoint,
            LegacyDofus210Messages.CreateIdentificationSuccessPacket(
                authentication.Account,
                _serverOptions.ServerCommunityId),
            cancellationToken);

        await SendPayloadAsync(
            stream,
            connectionId,
            remoteEndPoint,
            LegacyDofus210Messages.CreateServersListPacket(availableServers),
            cancellationToken);

        foreach (var availableServer in availableServers)
        {
            await SendPayloadAsync(
                stream,
                connectionId,
                remoteEndPoint,
                LegacyDofus210Messages.CreateServerStatusUpdatePacket(availableServer),
                cancellationToken);
        }

        _logger.LogInformation(
            "Identification accepted. ConnectionId={ConnectionId} Username={Username} AccountId={AccountId} PublishedServerIds={PublishedServerIds}",
            connectionId,
            authentication.Account.Username,
            authentication.Account.Id,
            string.Join(", ", availableServers.Select(server => server.Id)));
    }

    private async Task HandleServerSelectionAsync(
        NetworkStream stream,
        string connectionId,
        string remoteEndPoint,
        ReadOnlyMemory<byte> payload,
        AuthConnectionState state,
        IGameServerDirectoryService gameServerDirectoryService,
        CancellationToken cancellationToken)
    {
        var requestedServerId = LegacyDofus210Messages.ReadServerSelection(payload.Span);

        _logger.LogInformation(
            "Server selection received. ConnectionId={ConnectionId} RequestedServerId={ServerId}",
            connectionId,
            requestedServerId);

        if (state.Account is null)
        {
            _logger.LogWarning(
                "Server selection received before authentication. ConnectionId={ConnectionId}",
                connectionId);
            return;
        }

        var selectedServer = await gameServerDirectoryService.FindForAccountAsync(
            state.Account.Id,
            requestedServerId,
            cancellationToken);

        if (selectedServer is null)
        {
            await SendPayloadAsync(
                stream,
                connectionId,
                remoteEndPoint,
                LegacyDofus210Messages.CreateSelectedServerRefusedPacket(
                    requestedServerId,
                    1,
                    _serverOptions.GameServerStatus),
                cancellationToken);

            _logger.LogWarning(
                "Server selection refused. ConnectionId={ConnectionId} RequestedServerId={RequestedServerId}",
                connectionId,
                requestedServerId);

            state.CloseAfterCurrentPacket = true;

            return;
        }

        var ticketSession = _authTicketStore.Issue(
            state.Account,
            selectedServer.Id,
            _serverOptions.GameTicketTimeToLiveMinutes);

        await SendPayloadAsync(
            stream,
            connectionId,
            remoteEndPoint,
            LegacyDofus210Messages.CreateSelectedServerDataPacket(selectedServer, ticketSession),
            cancellationToken);

        _logger.LogInformation(
            "SelectedServerData sent. ConnectionId={ConnectionId} ServerId={ServerId} Ticket={Ticket} GameAddress={Address}:{Port}",
            connectionId,
            requestedServerId,
            ticketSession.Ticket,
            selectedServer.Address,
            selectedServer.Port);

        state.CloseAfterCurrentPacket = true;
    }

    private static bool ContainsPolicyFileRequest(string asciiPayload)
    {
        return asciiPayload.Contains("policy-file-request", StringComparison.OrdinalIgnoreCase);
    }

    private static string ClassifyPayload(byte[] payload, string asciiPayload)
    {
        if (ContainsPolicyFileRequest(asciiPayload))
        {
            return "FlashPolicyRequest";
        }

        var printableByteCount = payload.Count(currentByte =>
            currentByte is >= 32 and <= 126 || currentByte is 9 or 10 or 13);

        if (printableByteCount == payload.Length && printableByteCount > 0)
        {
            return "PlainTextCandidate";
        }

        if (payload.Length == 4)
        {
            return "BinaryHandshakeCandidate";
        }

        return "BinaryPayload";
    }

    private static bool TryExtractUsername(string asciiPayload, out string username)
    {
        username = string.Empty;

        var separators = new[] { '\0', '\n', '\r', '|', ';', ',', ' ', '\t' };
        var tokens = asciiPayload
            .Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length is >= 3 and <= 32)
            .ToList();

        username = tokens.FirstOrDefault(token =>
            token.All(static ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' or '.')) ?? string.Empty;

        return !string.IsNullOrWhiteSpace(username);
    }

    private static string ToHex(ReadOnlySpan<byte> payload)
    {
        return Convert.ToHexString(payload);
    }

    private string ResolveTranscriptPath()
    {
        if (Path.IsPathRooted(_serverOptions.AuthTranscriptDirectory))
        {
            return Path.Combine(_serverOptions.AuthTranscriptDirectory, "auth-transcript.log");
        }

        return Path.GetFullPath(
            Path.Combine(
                _hostEnvironment.ContentRootPath,
                _serverOptions.AuthTranscriptDirectory,
                "auth-transcript.log"));
    }

    private async Task SendPayloadAsync(
        NetworkStream stream,
        string connectionId,
        string remoteEndPoint,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        await stream.WriteAsync(payload, cancellationToken);
        await _trafficRecorder.RecordAsync(
            new AuthTrafficRecord(
                DateTimeOffset.UtcNow,
                connectionId,
                "OUT",
                remoteEndPoint,
                payload.Length,
                ToHex(payload),
                ToSanitizedAscii(payload)),
            cancellationToken);
    }

    private async Task RecordIncomingPayloadAsync(
        string connectionId,
        string remoteEndPoint,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        var asciiPayload = ToSanitizedAscii(payload);
        var hexPayload = ToHex(payload);

        await _trafficRecorder.RecordAsync(
            new AuthTrafficRecord(
                DateTimeOffset.UtcNow,
                connectionId,
                "IN",
                remoteEndPoint,
                payload.Length,
                hexPayload,
                asciiPayload),
            cancellationToken);

        _logger.LogInformation(
            "Auth payload received. ConnectionId={ConnectionId} Bytes={Bytes} Ascii={Ascii} Hex={Hex}",
            connectionId,
            payload.Length,
            asciiPayload,
            hexPayload);

        _logger.LogInformation(
            "Auth payload classified. ConnectionId={ConnectionId} Kind={Kind}",
            connectionId,
            ClassifyPayload(payload, asciiPayload));
    }

    private static string ToSanitizedAscii(ReadOnlySpan<byte> payload)
    {
        var builder = new StringBuilder(payload.Length);

        foreach (var currentByte in payload)
        {
            builder.Append(currentByte switch
            {
                >= 32 and <= 126 => (char)currentByte,
                9 => ' ',
                10 => ' ',
                13 => ' ',
                0 => ' ',
                _ => '.'
            });
        }

        return builder.ToString().Trim();
    }

    private static bool IsExpectedRemoteDisconnect(Exception exception)
    {
        var socketError = ExtractSocketError(exception);

        return socketError is SocketError.ConnectionReset
            or SocketError.ConnectionAborted
            or SocketError.OperationAborted
            or SocketError.Shutdown
            or SocketError.Interrupted;
    }

    private static SocketError? ExtractSocketError(Exception exception)
    {
        return exception switch
        {
            SocketException socketException => socketException.SocketErrorCode,
            IOException ioException when ioException.InnerException is SocketException socketException => socketException.SocketErrorCode,
            _ => null
        };
    }

    private sealed class AuthConnectionState
    {
        public bool BootstrapSent { get; set; }

        public string ClientKey { get; set; } = string.Empty;

        public AuthenticatedAccount? Account { get; set; }

        public bool CloseAfterCurrentPacket { get; set; }
    }
}
