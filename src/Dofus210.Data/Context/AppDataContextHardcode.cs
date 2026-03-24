using Dofus210.Data.Entities;
using Dofus210.Helper.EntityLook;

namespace Dofus210.Data.Context;

public static class AppDataContextHardcode
{
    public const string SqlServerConnectionString =
        "Server=DAGEMOV\\SQLEXPRESS;Database=Dofus2.10;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    // This map id was verified to exist in the local client map archives as 1/80217091.dlm.
    public const int DefaultSpawnMapId = 80217091;

    public const int LegacyInvalidSpawnMapId = 80217600;

    public const int InterimInvalidSpawnMapId = 80217607;

    public const short DefaultSpawnCellId = 300;

    public const byte DefaultSpawnDirection = 2;

    // Resolved from the local MapPositions.d2o entry for map 80217091.
    public const short DefaultSpawnSubAreaId = 445;

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
        CreateBreed(1, "Feca", "{1|10||135}", "{1|11||125}"),
        CreateBreed(2, "Osamodas", "{1|20||130}", "{1|21||125}"),
        CreateBreed(3, "Enutrof", "{1|30||120}", "{1|31||95}"),
        CreateBreed(4, "Sram", "{1|40||140}", "{1|41||155}"),
        CreateBreed(5, "Xelor", "{1|50||110}", "{1|51||110}"),
        CreateBreed(6, "Ecaflip", "{1|60||150}", "{1|61||150}"),
        CreateBreed(7, "Eniripsa", "{1|70||110}", "{1|71||115}"),
        CreateBreed(8, "Iop", "{1|80||140}", "{1|81||140}"),
        CreateBreed(9, "Cra", "{1|90||140}", "{1|91||135}"),
        CreateBreed(10, "Sadida", "{1|100||150}", "{1|101||145}"),
        CreateBreed(11, "Sacrieur", "{1|110||145}", "{1|111||140}"),
        CreateBreed(12, "Pandawa", "{1|120||160}", "{1|121||145}"),
        CreateBreed(13, "Roublard", "{1|1405||160}", "{1|1407||155}"),
        CreateBreed(14, "Zobal", "{1|1437||150}", "{1|1438||145}"),
        CreateBreed(15, "Steamer", "{1|1663||160}", "{1|1664||160}")
    ];

    public static IReadOnlyCollection<GameServer> GameServers { get; } =
    [
        new GameServer
        {
            Id = 4001,
            Name = "Henual",
            Address = "127.0.0.1",
            Port = 5556,
            CommunityId = 4,
            Type = 0,
            Status = 3,
            Completion = 0,
            CharacterCapacity = 5,
            CanCreateNewCharacter = true
        }
    ];

    private static Breed CreateBreed(byte id, string name, string maleLook, string femaleLook)
    {
        var maleDescriptor = LegacyBreedLookParser.Parse(maleLook);
        var femaleDescriptor = LegacyBreedLookParser.Parse(femaleLook);

        return new Breed
        {
            Id = id,
            Name = name,
            MaleLook = maleLook,
            FemaleLook = femaleLook,
            MaleBonesId = maleDescriptor.BonesId,
            FemaleBonesId = femaleDescriptor.BonesId,
            IsPlayable = true
        };
    }
}
