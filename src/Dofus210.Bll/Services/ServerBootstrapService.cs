using Dofus210.Bll.Models;
using Dofus210.Data.Context;
using Dofus210.Data.Entities;
using Dofus210.Data.UnitOfWork;
using Dofus210.Helper.Guards;

namespace Dofus210.Bll.Services;

public sealed class ServerBootstrapService : IServerBootstrapService
{
    private readonly IUnitOfWork _unitOfWork;

    public ServerBootstrapService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = Guard.AgainstNull(unitOfWork, nameof(unitOfWork));
    }

    public async Task<ServerBootstrapSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var accountRepository = _unitOfWork.Repository<Account>();
        var persistedAccountCount = await accountRepository.CountAsync(cancellationToken);

        return new ServerBootstrapSnapshot(
            persistedAccountCount,
            AppDataContextHardcode.Accounts.Count);
    }
}

