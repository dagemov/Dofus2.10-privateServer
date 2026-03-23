using Dofus210.Host.Options;
using Microsoft.Extensions.Options;

namespace Dofus210.Host.Game;

public sealed class FileGameTrafficRecorder : IGameTrafficRecorder
{
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly string _transcriptPath;

    public FileGameTrafficRecorder(IHostEnvironment hostEnvironment, IOptions<ServerOptions> serverOptions)
    {
        var transcriptDirectory = serverOptions.Value.GameTranscriptDirectory;

        _transcriptPath = Path.IsPathRooted(transcriptDirectory)
            ? Path.Combine(transcriptDirectory, "game-transcript.log")
            : Path.GetFullPath(Path.Combine(hostEnvironment.ContentRootPath, transcriptDirectory, "game-transcript.log"));

        Directory.CreateDirectory(Path.GetDirectoryName(_transcriptPath)!);
    }

    public async Task RecordAsync(GameTrafficRecord record, CancellationToken cancellationToken = default)
    {
        var line =
            $"{record.TimestampUtc:O} | {record.ConnectionId} | {record.Direction} | {record.RemoteEndPoint} | bytes={record.ByteCount} | hex={record.HexPayload}";

        await _mutex.WaitAsync(cancellationToken);

        try
        {
            await File.AppendAllTextAsync(_transcriptPath, line + Environment.NewLine, cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }
}
