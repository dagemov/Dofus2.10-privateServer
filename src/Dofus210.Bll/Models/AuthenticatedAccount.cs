namespace Dofus210.Bll.Models;

public sealed record AuthenticatedAccount(
    int Id,
    string Username,
    string Nickname,
    bool IsGameMaster,
    DateTime CreatedAtUtc);
