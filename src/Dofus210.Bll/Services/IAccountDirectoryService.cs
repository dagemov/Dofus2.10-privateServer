using Dofus210.Bll.Models;

namespace Dofus210.Bll.Services;

public interface IAccountDirectoryService
{
    Task<AccountSummary?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default);

    Task<AuthenticatedAccount?> ValidateCredentialsAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);
}
