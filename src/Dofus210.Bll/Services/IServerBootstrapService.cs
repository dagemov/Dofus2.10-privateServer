using Dofus210.Bll.Models;

namespace Dofus210.Bll.Services;

public interface IServerBootstrapService
{
    Task<ServerBootstrapSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}

