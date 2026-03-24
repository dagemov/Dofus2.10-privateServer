using Dofus210.Bll.Models;
using Dofus210.Data.Context;
using Dofus210.Data.Entities;
using Dofus210.Data.UnitOfWork;
using Dofus210.Helper.EntityLook;
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
        var rows = await _unitOfWork
            .Repository<Character>()
            .Query()
            .AsNoTracking()
            .Where(character => character.AccountId == accountId && character.GameServerId == gameServerId)
            .OrderBy(character => character.CreatedAtUtc)
            .Select(character => new
            {
                character.Id,
                character.Name,
                character.Level,
                character.BreedId,
                character.Sex,
                character.BonesId,
                character.SkinId,
                character.CosmeticId,
                character.Breed.MaleLook,
                character.Breed.FemaleLook,
                character.Color1,
                character.Color2,
                character.Color3,
                character.Color4,
                character.Color5
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(character => CreateCharacterSummary(
                character.Id,
                character.Name,
                character.Level,
                character.BreedId,
                character.Sex,
                character.BonesId,
                character.SkinId,
                character.CosmeticId,
                character.MaleLook,
                character.FemaleLook,
                character.Color1,
                character.Color2,
                character.Color3,
                character.Color4,
                character.Color5))
            .ToArray();
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
        var breedLook = LegacyBreedLookParser.Parse(request.Sex ? breed.FemaleLook : breed.MaleLook);

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
            BonesId = breedLook.BonesId,
            SkinId = breedLook.PrimarySkinId,
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
            CreateCharacterSummary(
                character.Id,
                character.Name,
                character.Level,
                character.BreedId,
                character.Sex,
                character.BonesId,
                character.SkinId,
                character.CosmeticId,
                breed.MaleLook,
                breed.FemaleLook,
                colors[0],
                colors[1],
                colors[2],
                colors[3],
                colors[4]));
    }

    public async Task<CharacterSelectionContext?> GetSelectionContextAsync(
        int accountId,
        short gameServerId,
        long characterId,
        CancellationToken cancellationToken = default)
    {
        var character = await _unitOfWork
            .Repository<Character>()
            .Query()
            .AsNoTracking()
            .Include(current => current.Stats)
            .Include(current => current.Position)
            .Include(current => current.Breed)
            .FirstOrDefaultAsync(
                current =>
                    current.Id == characterId &&
                    current.AccountId == accountId &&
                    current.GameServerId == gameServerId,
                cancellationToken);

        if (character is null)
        {
            return null;
        }

        var stats = character.Stats;
        var position = character.Position;
        var indexedColors = new[]
        {
            character.Color1,
            character.Color2,
            character.Color3,
            character.Color4,
            character.Color5
        }
        .Where(color => color > 0)
        .ToArray();

        var normalizedMapId = character.Position?.MapId is > 0 &&
                              character.Position.MapId != AppDataContextHardcode.LegacyInvalidSpawnMapId &&
                              character.Position.MapId != AppDataContextHardcode.InterimInvalidSpawnMapId
            ? character.Position.MapId
            : AppDataContextHardcode.DefaultSpawnMapId;

        var normalizedCellId = position?.CellId is >= 0 and <= 559
            ? position.CellId
            : AppDataContextHardcode.DefaultSpawnCellId;

        var normalizedDirection = position?.Direction is > 0 and <= 7
            ? position.Direction
            : AppDataContextHardcode.DefaultSpawnDirection;

        return new CharacterSelectionContext(
            CreateCharacterSummary(
                character.Id,
                character.Name,
                character.Level,
                character.BreedId,
                character.Sex,
                character.BonesId,
                character.SkinId,
                character.CosmeticId,
                character.Breed.MaleLook,
                character.Breed.FemaleLook,
                character.Color1,
                character.Color2,
                character.Color3,
                character.Color4,
                character.Color5),
            character.Experience,
            stats?.Kamas ?? 0,
            stats?.StatsPoints ?? 0,
            stats?.SpellsPoints ?? 0,
            stats?.LifePoints ?? 50,
            stats?.MaxLifePoints ?? 50,
            stats?.EnergyPoints ?? 10000,
            stats?.MaxEnergyPoints ?? 10000,
            stats?.ActionPoints ?? 6,
            stats?.MovementPoints ?? 3,
            normalizedMapId,
            normalizedCellId,
            normalizedDirection,
            AppDataContextHardcode.DefaultSpawnSubAreaId);
    }

    public async Task<CharacterSelectionContext?> UpdatePositionAsync(
        int accountId,
        short gameServerId,
        long characterId,
        int mapId,
        short cellId,
        byte direction,
        CancellationToken cancellationToken = default)
    {
        var character = await _unitOfWork
            .Repository<Character>()
            .Query()
            .Include(current => current.Position)
            .FirstOrDefaultAsync(
                current =>
                    current.Id == characterId &&
                    current.AccountId == accountId &&
                    current.GameServerId == gameServerId,
                cancellationToken);

        if (character is null)
        {
            return null;
        }

        character.Position ??= new CharacterPosition
        {
            CharacterId = character.Id
        };

        character.Position.MapId = mapId > 0 ? mapId : AppDataContextHardcode.DefaultSpawnMapId;
        character.Position.CellId = cellId >= 0 ? cellId : AppDataContextHardcode.DefaultSpawnCellId;
        character.Position.Direction = direction is > 0 and <= 7
            ? direction
            : AppDataContextHardcode.DefaultSpawnDirection;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetSelectionContextAsync(
            accountId,
            gameServerId,
            characterId,
            cancellationToken);
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

    private static CharacterSummary CreateCharacterSummary(
        long id,
        string name,
        short level,
        byte breedId,
        bool sex,
        int storedBonesId,
        int storedSkinId,
        short cosmeticId,
        string maleLook,
        string femaleLook,
        int color1,
        int color2,
        int color3,
        int color4,
        int color5)
    {
        var breedLook = LegacyBreedLookParser.Parse(sex ? femaleLook : maleLook);
        var indexedColors = new[]
        {
            color1,
            color2,
            color3,
            color4,
            color5
        }
        .Where(color => color > 0)
        .ToArray();

        var normalizedSkinId = breedLook.PrimarySkinId > 0
            ? breedLook.PrimarySkinId
            : storedSkinId;

        var normalizedBonesId = breedLook.BonesId > 0
            ? breedLook.BonesId
            : (storedBonesId > 0 ? storedBonesId : 1);

        return new CharacterSummary(
            id,
            name,
            level,
            breedId,
            sex,
            normalizedBonesId,
            normalizedSkinId,
            breedLook.ScalePercent,
            cosmeticId,
            indexedColors);
    }
}
