using Dofus210.Bll.Models;
using Dofus210.Data.Entities;
using Dofus210.Data.UnitOfWork;
using Dofus210.Helper.Guards;
using Microsoft.EntityFrameworkCore;

namespace Dofus210.Bll.Services;

public sealed class GameServerDirectoryService : IGameServerDirectoryService
{
    private readonly IUnitOfWork _unitOfWork;

    public GameServerDirectoryService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = Guard.AgainstNull(unitOfWork, nameof(unitOfWork));
    }

    public async Task<IReadOnlyList<GameServerSummary>> ListForAccountAsync(
        int accountId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _unitOfWork
            .Repository<GameServer>()
            .Query()
            .AsNoTracking()
            .OrderBy(server => server.Id)
            .Select(server => new
            {
                server.Id,
                server.Name,
                server.Address,
                server.Port,
                server.CommunityId,
                server.Type,
                server.Status,
                server.Completion,
                server.CharacterCapacity,
                server.CanCreateNewCharacter,
                CharactersCount = server.Characters.Count(character => character.AccountId == accountId)
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new GameServerSummary(
                row.Id,
                row.Name,
                row.Address,
                row.Port,
                row.CommunityId,
                row.Type,
                row.Status,
                row.Completion,
                row.CharacterCapacity,
                row.CanCreateNewCharacter,
                (byte)Math.Min(row.CharactersCount, byte.MaxValue)))
            .ToArray();
    }

    public async Task<GameServerSummary?> FindForAccountAsync(
        int accountId,
        short gameServerId,
        CancellationToken cancellationToken = default)
    {
        var row = await _unitOfWork
            .Repository<GameServer>()
            .Query()
            .AsNoTracking()
            .Where(server => server.Id == gameServerId)
            .Select(server => new
            {
                server.Id,
                server.Name,
                server.Address,
                server.Port,
                server.CommunityId,
                server.Type,
                server.Status,
                server.Completion,
                server.CharacterCapacity,
                server.CanCreateNewCharacter,
                CharactersCount = server.Characters.Count(character => character.AccountId == accountId)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        return new GameServerSummary(
            row.Id,
            row.Name,
            row.Address,
            row.Port,
            row.CommunityId,
            row.Type,
            row.Status,
            row.Completion,
            row.CharacterCapacity,
            row.CanCreateNewCharacter,
            (byte)Math.Min(row.CharactersCount, byte.MaxValue));
    }
}
