namespace Dofus210.Bll.Models;

public sealed record CharacterSummary(
    long Id,
    string Name,
    short Level,
    byte BreedId,
    bool Sex,
    int BonesId,
    int SkinId,
    int ScalePercent,
    short CosmeticId,
    IReadOnlyList<int> IndexedColors);
