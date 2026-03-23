using Dofus210.Bll.Models;
using Dofus210.Data.Entities;
using Dofus210.Data.UnitOfWork;
using Dofus210.Helper.Guards;

namespace Dofus210.Bll.Services;

public sealed class AccountDirectoryService : IAccountDirectoryService
{
    private readonly IUnitOfWork _unitOfWork;

    public AccountDirectoryService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = Guard.AgainstNull(unitOfWork, nameof(unitOfWork));
    }

    public async Task<AccountSummary?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var normalizedUsername = Guard.AgainstNullOrWhiteSpace(username, nameof(username));

        var account = await _unitOfWork
            .Repository<Account>()
            .FirstOrDefaultAsync(x => x.Username == normalizedUsername, cancellationToken);

        if (account is null)
        {
            return null;
        }

        return new AccountSummary(
            account.Id,
            account.Username,
            account.Nickname,
            account.IsGameMaster);
    }

    public async Task<AuthenticatedAccount?> ValidateCredentialsAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var normalizedUsername = Guard.AgainstNullOrWhiteSpace(username, nameof(username));
        var normalizedPassword = Guard.AgainstNullOrWhiteSpace(password, nameof(password));

        var account = await _unitOfWork
            .Repository<Account>()
            .FirstOrDefaultAsync(
                x => x.Username == normalizedUsername && x.PasswordHash == normalizedPassword,
                cancellationToken);

        if (account is null)
        {
            return null;
        }

        return new AuthenticatedAccount(
            account.Id,
            account.Username,
            account.Nickname,
            account.IsGameMaster,
            account.CreatedAtUtc);
    }
}
