using Dofus210.Bll.Models;
using Dofus210.Data.Context;
using Dofus210.Data.Entities;
using Dofus210.Data.UnitOfWork;
using Dofus210.Helper.Guards;
using Microsoft.EntityFrameworkCore;

namespace Dofus210.Bll.Services;

public sealed class CharacterDirectoryService : ICharacterDirectoryService
{
    private const byte ResultOk = 0;
    private const byte ResultNoReason = 1;
    private const byte ResultInvalidName = 2;
    private const byte ResultNameAlreadyExists = 3;
    private const byte ResultTooManyCharacters = 4;

    private readonly IUnitOfWork _unitOfWork;

    public CharacterDirectoryService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = Guard.AgainstNull(unitOfWork, nameof(unitOfWork));
    }

    public async Task<IReadOnlyList<CharacterSummary>> ListForAccountAsync(
        int accountId,
        short gameServerId,
        CancellationToken cancellationToken = default)
    {
        var characters = await _unitOfWork
            .Repository<Character>()
            .Query()
            .AsNoTracking()
            .Where(character => character.AccountId == accountId && character.GameServerId == gameServerId)
            .OrderBy(character => character.CreatedAtUtc)
            .Select(character => new CharacterSummary(
                character.Id,
                character.Name,
                character.Level,
                character.BreedId,
                character.Sex,
                character.BonesId,
                character.SkinId,
                character.CosmeticId,
                new[]
                {
                    character.Color1,
                    character.Color2,
                    character.Color3,
                    character.Color4,
                    character.Color5
                }
                .Where(color => color > 0)
                .ToArray()))
            .ToListAsync(cancellationToken);

        return characters;
    }

    public async Task<bool> CanCreateAsync(
        int accountId,
        short gameServerId,
        CancellationToken cancellationToken = default)
    {
        var server = await _unitOfWork
            .Repository<GameServer>()
            .FirstOrDefaultAsync(x => x.Id == gameServerId, cancellationToken);

        if (server is null || !server.CanCreateNewCharacter)
        {
            return false;
        }

        var characterCount = await _unitOfWork
            .Repository<Character>()
            .Query()
            .AsNoTracking()
            .CountAsync(
                character => character.AccountId == accountId && character.GameServerId == gameServerId,
                cancellationToken);

        return characterCount < server.CharacterCapacity;
    }

    public async Task<CharacterCreationResult> CreateAsync(
        int accountId,
        short gameServerId,
        CharacterCreationRequest request,
        CancellationToken cancellationToken = default)
    {
        Guard.AgainstNull(request, nameof(request));

        var normalizedName = request.Name.Trim();

        if (!IsValidCharacterName(normalizedName))
        {
            return new CharacterCreationResult(ResultInvalidName, null);
        }

        if (!await CanCreateAsync(accountId, gameServerId, cancellationToken))
        {
            return new CharacterCreationResult(ResultTooManyCharacters, null);
        }

        var breed = await _unitOfWork
            .Repository<Breed>()
            .FirstOrDefaultAsync(x => x.Id == request.BreedId && x.IsPlayable, cancellationToken);

        if (breed is null)
        {
            return new CharacterCreationResult(ResultNoReason, null);
        }

        var existingName = await _unitOfWork
            .Repository<Character>()
            .AnyAsync(
                x => x.GameServerId == gameServerId && x.Name == normalizedName,
                cancellationToken);

        if (existingName)
        {
            return new CharacterCreationResult(ResultNameAlreadyExists, null);
        }

        var colors = NormalizeColors(request.Colors);
        var bonesId = request.Sex ? breed.FemaleBonesId : breed.MaleBonesId;

        var character = new Character
        {
            AccountId = accountId,
            GameServerId = gameServerId,
            BreedId = request.BreedId,
            Name = normalizedName,
            Sex = request.Sex,
            Level = 1,
            Experience = 0,
            CosmeticId = request.CosmeticId,
            Color1 = colors[0],
            Color2 = colors[1],
            Color3 = colors[2],
            Color4 = colors[3],
            Color5 = colors[4],
            BonesId = bonesId <= 0 ? 1 : bonesId,
            SkinId = request.CosmeticId > 0 ? request.CosmeticId : 0,
            CreatedAtUtc = DateTime.UtcNow,
            Stats = new CharacterStats
            {
                Kamas = 0,
                StatsPoints = 0,
                SpellsPoints = 0,
                LifePoints = 50,
                MaxLifePoints = 50,
                EnergyPoints = 10000,
                MaxEnergyPoints = 10000,
                ActionPoints = 6,
                MovementPoints = 3
            },
            Position = new CharacterPosition
            {
                MapId = AppDataContextHardcode.DefaultSpawnMapId,
                CellId = AppDataContextHardcode.DefaultSpawnCellId,
                Direction = 2
            }
        };

        await _unitOfWork.Repository<Character>().AddAsync(character, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CharacterCreationResult(
            ResultOk,
            new CharacterSummary(
                character.Id,
                character.Name,
                character.Level,
                character.BreedId,
                character.Sex,
                character.BonesId,
                character.SkinId,
                character.CosmeticId,
                colors.Where(color => color > 0).ToArray()));
    }

    private static bool IsValidCharacterName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length is < 3 or > 20)
        {
            return false;
        }

        if (!char.IsLetter(value[0]))
        {
            return false;
        }

        return value.All(character => char.IsLetter(character) || character == '-');
    }

    private static int[] NormalizeColors(IReadOnlyList<int> colors)
    {
        var normalized = new int[5];

        for (var index = 0; index < normalized.Length; index++)
        {
            normalized[index] = index < colors.Count ? colors[index] : 0;
        }

        return normalized;
    }
}
