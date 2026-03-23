namespace Dofus210.Bll.Models;

public sealed record CharacterCreationRequest(
    string Name,
    byte BreedId,
    bool Sex,
    IReadOnlyList<int> Colors,
    short CosmeticId);
