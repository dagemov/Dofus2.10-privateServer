using Dofus210.Data.Entities;

namespace Dofus210.Data.Context;

public static class AppDataContextHardcode
{
    public const string SqlServerConnectionString =
        "Server=DAGEMOV\\SQLEXPRESS;Database=Dofus2.10;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    // This is a placeholder map id sampled from the local client map archives.
    public const int DefaultSpawnMapId = 80217600;

    public const short DefaultSpawnCellId = 300;

    public const byte DefaultSpawnDirection = 2;

    public const short DefaultSpawnSubAreaId = 0;

    public static IReadOnlyCollection<Account> Accounts { get; } =
    [
        new Account
        {
            Id = 1,
            Username = "admin",
            PasswordHash = "CHANGE_ME",
            Nickname = "Administrator",
            IsGameMaster = true,
            CreatedAtUtc = new DateTime(2026, 03, 22, 0, 0, 0, DateTimeKind.Utc)
        },
         new Account
        {
            Id = 2,
            Username = "sebcos1",
            PasswordHash = "polondrolo3",
            Nickname = "Administrator",
            IsGameMaster = true,
            CreatedAtUtc = new DateTime(2026, 03, 22, 0, 0, 0, DateTimeKind.Utc)
        }
    ];

    public static IReadOnlyCollection<Breed> Breeds { get; } =
    [
        CreateBreed(1, "Feca"),
        CreateBreed(2, "Osamodas"),
        CreateBreed(3, "Enutrof"),
        CreateBreed(4, "Sram"),
        CreateBreed(5, "Xelor"),
        CreateBreed(6, "Ecaflip"),
        CreateBreed(7, "Eniripsa"),
        CreateBreed(8, "Iop"),
        CreateBreed(9, "Cra"),
        CreateBreed(10, "Sadida"),
        CreateBreed(11, "Sacrieur"),
        CreateBreed(12, "Pandawa"),
        CreateBreed(13, "Roublard"),
        CreateBreed(14, "Zobal"),
        CreateBreed(15, "Steamer")
    ];

    public static IReadOnlyCollection<GameServer> GameServers { get; } =
    [
        new GameServer
        {
            Id = 1,
            Name = "Rushu",
            Address = "127.0.0.1",
            Port = 5556,
            CommunityId = 0,
            Type = 0,
            Status = 3,
            Completion = 0,
            CharacterCapacity = 5,
            CanCreateNewCharacter = true
        },
        new GameServer
        {
            Id = 4001,
            Name = "Henual",
            Address = "127.0.0.1",
            Port = 5556,
            CommunityId = 0,
            Type = 0,
            Status = 3,
            Completion = 0,
            CharacterCapacity = 5,
            CanCreateNewCharacter = true
        }
    ];

    private static Breed CreateBreed(byte id, string name)
    {
        // These look values are placeholders until the D2O importer is in place.
        return new Breed
        {
            Id = id,
            Name = name,
            MaleLook = string.Empty,
            FemaleLook = string.Empty,
            MaleBonesId = id,
            FemaleBonesId = id,
            IsPlayable = true
        };
    }
}
