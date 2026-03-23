namespace Dofus210.Bll.Models;

public sealed record AccountSummary(
    int Id,
    string Username,
    string Nickname,
    bool IsGameMaster);

