using Dofus210.Bll.Models;

namespace Dofus210.Bll.Services;

public interface IGameServerDirectoryService
{
    Task<IReadOnlyList<GameServerSummary>> ListForAccountAsync(
        int accountId,
        CancellationToken cancellationToken = default);

    Task<GameServerSummary?> FindForAccountAsync(
        int accountId,
        short gameServerId,
        CancellationToken cancellationToken = default);
}
