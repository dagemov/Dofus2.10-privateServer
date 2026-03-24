using Dofus210.Data.Entities;
using Dofus210.Helper.EntityLook;
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
        await SynchronizeHardcodedBreedsAsync(cancellationToken);
        await SynchronizeHardcodedGameServersAsync(cancellationToken);
        await NormalizeCharacterLooksAsync(cancellationToken);
        await NormalizeSpawnPositionsAsync(cancellationToken);

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

    private async Task SynchronizeHardcodedBreedsAsync(CancellationToken cancellationToken)
    {
        var existingBreeds = await _context.Breeds
            .ToDictionaryAsync(breed => breed.Id, cancellationToken);

        var changesPending = false;

        foreach (var hardcodedBreed in AppDataContextHardcode.Breeds)
        {
            if (existingBreeds.TryGetValue(hardcodedBreed.Id, out var persistedBreed))
            {
                changesPending |= CopyValuesIfNeeded(persistedBreed, hardcodedBreed);
                continue;
            }

            _context.Breeds.Add(new Breed
            {
                Id = hardcodedBreed.Id,
                Name = hardcodedBreed.Name,
                MaleLook = hardcodedBreed.MaleLook,
                FemaleLook = hardcodedBreed.FemaleLook,
                MaleBonesId = hardcodedBreed.MaleBonesId,
                FemaleBonesId = hardcodedBreed.FemaleBonesId,
                IsPlayable = hardcodedBreed.IsPlayable
            });

            changesPending = true;
        }

        if (changesPending)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task SynchronizeHardcodedGameServersAsync(CancellationToken cancellationToken)
    {
        var existingServers = await _context.GameServers
            .ToDictionaryAsync(server => server.Id, cancellationToken);

        var changesPending = false;
        var serversToInsert = new List<GameServer>();

        foreach (var hardcodedServer in AppDataContextHardcode.GameServers)
        {
            if (existingServers.TryGetValue(hardcodedServer.Id, out var persistedServer))
            {
                changesPending |= CopyValuesIfNeeded(persistedServer, hardcodedServer);
                continue;
            }

            serversToInsert.Add(new GameServer
            {
                Id = hardcodedServer.Id,
                Name = hardcodedServer.Name,
                Address = hardcodedServer.Address,
                Port = hardcodedServer.Port,
                CommunityId = hardcodedServer.CommunityId,
                Type = hardcodedServer.Type,
                Status = hardcodedServer.Status,
                Completion = hardcodedServer.Completion,
                CharacterCapacity = hardcodedServer.CharacterCapacity,
                CanCreateNewCharacter = hardcodedServer.CanCreateNewCharacter
            });
        }

        if (changesPending)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        if (serversToInsert.Count == 0)
        {
            return;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await _context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [GameServers] ON;", cancellationToken);

        try
        {
            _context.GameServers.AddRange(serversToInsert);
            await _context.SaveChangesAsync(cancellationToken);
            await _context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [GameServers] OFF;", cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await _context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [GameServers] OFF;", cancellationToken);
            throw;
        }
    }

    private async Task NormalizeCharacterLooksAsync(CancellationToken cancellationToken)
    {
        var characters = await _context.Characters
            .Include(character => character.Breed)
            .ToListAsync(cancellationToken);

        var changesPending = false;

        foreach (var character in characters)
        {
            if (character.Breed is null)
            {
                continue;
            }

            var descriptor = LegacyBreedLookParser.Parse(
                character.Sex
                    ? character.Breed.FemaleLook
                    : character.Breed.MaleLook);

            if (character.BonesId <= 0 && descriptor.BonesId > 0)
            {
                character.BonesId = descriptor.BonesId;
                changesPending = true;
            }

            if (character.SkinId <= 0 && descriptor.PrimarySkinId > 0)
            {
                character.SkinId = descriptor.PrimarySkinId;
                changesPending = true;
            }
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

    private static bool CopyValuesIfNeeded(Breed target, Breed source)
    {
        var changed = false;

        if (!string.Equals(target.Name, source.Name, StringComparison.Ordinal))
        {
            target.Name = source.Name;
            changed = true;
        }

        if (!string.Equals(target.MaleLook, source.MaleLook, StringComparison.Ordinal))
        {
            target.MaleLook = source.MaleLook;
            changed = true;
        }

        if (!string.Equals(target.FemaleLook, source.FemaleLook, StringComparison.Ordinal))
        {
            target.FemaleLook = source.FemaleLook;
            changed = true;
        }

        if (target.MaleBonesId != source.MaleBonesId)
        {
            target.MaleBonesId = source.MaleBonesId;
            changed = true;
        }

        if (target.FemaleBonesId != source.FemaleBonesId)
        {
            target.FemaleBonesId = source.FemaleBonesId;
            changed = true;
        }

        if (target.IsPlayable != source.IsPlayable)
        {
            target.IsPlayable = source.IsPlayable;
            changed = true;
        }

        return changed;
    }

    private static bool CopyValuesIfNeeded(GameServer target, GameServer source)
    {
        var changed = false;

        if (!string.Equals(target.Name, source.Name, StringComparison.Ordinal))
        {
            target.Name = source.Name;
            changed = true;
        }

        if (!string.Equals(target.Address, source.Address, StringComparison.Ordinal))
        {
            target.Address = source.Address;
            changed = true;
        }

        if (target.Port != source.Port)
        {
            target.Port = source.Port;
            changed = true;
        }

        if (target.CommunityId != source.CommunityId)
        {
            target.CommunityId = source.CommunityId;
            changed = true;
        }

        if (target.Type != source.Type)
        {
            target.Type = source.Type;
            changed = true;
        }

        if (target.Status != source.Status)
        {
            target.Status = source.Status;
            changed = true;
        }

        if (target.Completion != source.Completion)
        {
            target.Completion = source.Completion;
            changed = true;
        }

        if (target.CharacterCapacity != source.CharacterCapacity)
        {
            target.CharacterCapacity = source.CharacterCapacity;
            changed = true;
        }

        if (target.CanCreateNewCharacter != source.CanCreateNewCharacter)
        {
            target.CanCreateNewCharacter = source.CanCreateNewCharacter;
            changed = true;
        }

        return changed;
    }

    private async Task NormalizeSpawnPositionsAsync(CancellationToken cancellationToken)
    {
        var positions = await _context.CharacterPositions
            .Where(position =>
                position.MapId == AppDataContextHardcode.LegacyInvalidSpawnMapId ||
                position.MapId == AppDataContextHardcode.InterimInvalidSpawnMapId ||
                position.MapId <= 0 ||
                position.CellId < 0 ||
                position.CellId > 559 ||
                position.Direction == 0 ||
                position.Direction > 7)
            .ToListAsync(cancellationToken);

        if (positions.Count == 0)
        {
            return;
        }

        foreach (var position in positions)
        {
            position.MapId = AppDataContextHardcode.DefaultSpawnMapId;
            position.CellId = AppDataContextHardcode.DefaultSpawnCellId;
            position.Direction = AppDataContextHardcode.DefaultSpawnDirection;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
