using System.Net;
using System.Net.Sockets;
using System.Text;
using Dofus210.Host.Networking;
using Dofus210.Host.Options;
using Microsoft.Extensions.Options;

namespace Dofus210.Host.HostedServices;

public sealed class SocketPolicyHostedService : BackgroundService
{
    private static readonly byte[] FlashPolicyResponse = Encoding.ASCII.GetBytes(
        "<?xml version=\"1.0\"?><cross-domain-policy><allow-access-from domain=\"*\" to-ports=\"*\" /></cross-domain-policy>\0");

    private readonly ILogger<SocketPolicyHostedService> _logger;
    private readonly ServerOptions _serverOptions;
    private TcpListener? _listener;

    public SocketPolicyHostedService(
        IOptions<ServerOptions> serverOptions,
        ILogger<SocketPolicyHostedService> logger)
    {
        _logger = logger;
        _serverOptions = serverOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_serverOptions.EnableSocketPolicyServer)
        {
            _logger.LogInformation("Socket policy listener disabled.");
            return;
        }

        var listenAddress = NetworkEndpointResolver.ResolveListenAddress(_serverOptions.Host);
        _listener = new TcpListener(listenAddress, _serverOptions.SocketPolicyPort);
        _listener.Server.NoDelay = true;
        _listener.Start();

        _logger.LogInformation(
            "Socket policy listener started on {Host}:{Port}.",
            listenAddress,
            _serverOptions.SocketPolicyPort);

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
            _logger.LogInformation("Socket policy listener stopped.");
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _listener?.Stop();
        return base.StopAsync(cancellationToken);
    }

    private async Task HandleClientAsync(TcpClient tcpClient, CancellationToken cancellationToken)
    {
        using (tcpClient)
        {
            tcpClient.NoDelay = true;

            try
            {
                using var stream = tcpClient.GetStream();
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));
                var buffer = new byte[512];
                var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), timeoutCts.Token);

                if (bytesRead <= 0)
                {
                    return;
                }

                var asciiPayload = Encoding.ASCII.GetString(buffer, 0, bytesRead);

                if (!asciiPayload.Contains("policy-file-request", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug(
                        "Socket policy listener ignored unexpected payload. Bytes={Bytes} Ascii={Ascii}",
                        bytesRead,
                        Sanitize(asciiPayload));
                    return;
                }

                await stream.WriteAsync(FlashPolicyResponse, cancellationToken);
                _logger.LogInformation("Socket policy response sent.");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Socket policy request timed out.");
            }
            catch (IOException exception) when (IsExpectedRemoteDisconnect(exception))
            {
                _logger.LogDebug("Socket policy client disconnected abruptly. Message={Message}", exception.Message);
            }
            catch (SocketException exception) when (IsExpectedRemoteDisconnect(exception))
            {
                _logger.LogDebug("Socket policy socket disconnected abruptly. SocketError={SocketError}", exception.SocketErrorCode);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Socket policy session failed.");
            }
        }
    }

    private static bool IsExpectedRemoteDisconnect(Exception exception)
    {
        return exception switch
        {
            IOException ioException when ioException.InnerException is SocketException socketException => IsExpectedRemoteDisconnect(socketException),
            SocketException socketException => socketException.SocketErrorCode is SocketError.ConnectionReset or SocketError.ConnectionAborted,
            _ => false
        };
    }

    private static string Sanitize(string payload)
    {
        return string.Create(payload.Length, payload, static (span, source) =>
        {
            for (var index = 0; index < source.Length; index++)
            {
                var current = source[index];
                span[index] = current is >= ' ' and <= '~' ? current : '.';
            }
        });
    }
}
