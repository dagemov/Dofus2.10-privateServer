using System.Text;
using Dofus210.Host.Options;
using Microsoft.Extensions.Options;

namespace Dofus210.Host.Auth;

public sealed class FileAuthTrafficRecorder : IAuthTrafficRecorder
{
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<FileAuthTrafficRecorder> _logger;
    private readonly ServerOptions _serverOptions;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public FileAuthTrafficRecorder(
        IHostEnvironment hostEnvironment,
        IOptions<ServerOptions> serverOptions,
        ILogger<FileAuthTrafficRecorder> logger)
    {
        _hostEnvironment = hostEnvironment;
        _logger = logger;
        _serverOptions = serverOptions.Value;
    }

    public async Task RecordAsync(AuthTrafficRecord record, CancellationToken cancellationToken = default)
    {
        var transcriptDirectory = ResolveTranscriptDirectory();
        Directory.CreateDirectory(transcriptDirectory);

        var transcriptPath = Path.Combine(transcriptDirectory, "auth-transcript.log");
        var line = $"{record.TimestampUtc:O} | {record.ConnectionId} | {record.Direction} | {record.RemoteEndPoint} | bytes={record.ByteCount} | ascii={record.AsciiPayload} | hex={record.HexPayload}{Environment.NewLine}";

        await _gate.WaitAsync(cancellationToken);

        try
        {
            await File.AppendAllTextAsync(transcriptPath, line, Encoding.UTF8, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }

        _logger.LogDebug(
            "Auth transcript saved. ConnectionId={ConnectionId} Direction={Direction} Bytes={Bytes}",
            record.ConnectionId,
            record.Direction,
            record.ByteCount);
    }

    private string ResolveTranscriptDirectory()
    {
        if (Path.IsPathRooted(_serverOptions.AuthTranscriptDirectory))
        {
            return _serverOptions.AuthTranscriptDirectory;
        }

        return Path.Combine(_hostEnvironment.ContentRootPath, _serverOptions.AuthTranscriptDirectory);
    }
}

