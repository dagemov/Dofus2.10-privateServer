namespace Dofus210.Host.Auth;

public interface IAuthTrafficRecorder
{
    Task RecordAsync(AuthTrafficRecord record, CancellationToken cancellationToken = default);
}

