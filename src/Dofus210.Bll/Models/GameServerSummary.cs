namespace Dofus210.Bll.Models;

public sealed record GameServerSummary(
    short Id,
    string Name,
    string Address,
    int Port,
    byte CommunityId,
    byte Type,
    byte Status,
    byte Completion,
    byte CharacterCapacity,
    bool CanCreateNewCharacter,
    byte CharactersCount);
