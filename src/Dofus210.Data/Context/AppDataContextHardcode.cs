using Dofus210.Data.Entities;

namespace Dofus210.Data.Context;

public static class AppDataContextHardcode
{
    public const string SqlServerConnectionString =
        "Server=DAGEMOV\\SQLEXPRESS;Database=Dofus2.10;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

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
}

