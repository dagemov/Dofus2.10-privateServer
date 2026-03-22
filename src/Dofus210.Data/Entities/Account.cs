namespace Dofus210.Data.Entities;

public sealed class Account
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string Nickname { get; set; } = string.Empty;

    public bool IsGameMaster { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

