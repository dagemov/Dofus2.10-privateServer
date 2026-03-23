namespace Dofus210.Host.Game;

public interface IGameTrafficRecorder
{
    Task RecordAsync(GameTrafficRecord record, CancellationToken cancellationToken = default);
}
