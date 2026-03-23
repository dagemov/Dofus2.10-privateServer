using Dofus210.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dofus210.Data.Context;

public sealed class AppDataContextInitializer : IAppDataContextInitializer
{
    private readonly AppDataContext _context;

    public AppDataContextInitializer(AppDataContext context)
    {
        _context = context;
    }

    public async Task<bool> EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        var databaseCreated = await _context.Database.EnsureCreatedAsync(cancellationToken);
        await SynchronizeHardcodedAccountsAsync(cancellationToken);

        return databaseCreated;
    }

    private async Task SynchronizeHardcodedAccountsAsync(CancellationToken cancellationToken)
    {
        var existingAccounts = await _context.Accounts
            .ToDictionaryAsync(account => account.Username, StringComparer.Ordinal, cancellationToken);

        var changesPending = false;

        foreach (var hardcodedAccount in AppDataContextHardcode.Accounts)
        {
            if (existingAccounts.TryGetValue(hardcodedAccount.Username, out var persistedAccount))
            {
                changesPending |= CopyValuesIfNeeded(persistedAccount, hardcodedAccount);
                continue;
            }

            _context.Accounts.Add(new Account
            {
                Username = hardcodedAccount.Username,
                PasswordHash = hardcodedAccount.PasswordHash,
                Nickname = hardcodedAccount.Nickname,
                IsGameMaster = hardcodedAccount.IsGameMaster,
                CreatedAtUtc = hardcodedAccount.CreatedAtUtc
            });

            changesPending = true;
        }

        if (changesPending)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private static bool CopyValuesIfNeeded(Account target, Account source)
    {
        var changed = false;

        if (!string.Equals(target.PasswordHash, source.PasswordHash, StringComparison.Ordinal))
        {
            target.PasswordHash = source.PasswordHash;
            changed = true;
        }

        if (!string.Equals(target.Nickname, source.Nickname, StringComparison.Ordinal))
        {
            target.Nickname = source.Nickname;
            changed = true;
        }

        if (target.IsGameMaster != source.IsGameMaster)
        {
            target.IsGameMaster = source.IsGameMaster;
            changed = true;
        }

        if (target.CreatedAtUtc != source.CreatedAtUtc)
        {
            target.CreatedAtUtc = source.CreatedAtUtc;
            changed = true;
        }

        return changed;
    }
}
