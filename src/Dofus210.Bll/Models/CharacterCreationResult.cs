namespace Dofus210.Bll.Models;

public sealed record CharacterCreationResult(
    byte ResultCode,
    CharacterSummary? Character);
