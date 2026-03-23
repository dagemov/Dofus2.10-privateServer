namespace Dofus210.Data.Entities;

public sealed class Character
{
    public long Id { get; set; }

    public int AccountId { get; set; }

    public short GameServerId { get; set; }

    public byte BreedId { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool Sex { get; set; }

    public short Level { get; set; } = 1;

    public long Experience { get; set; }

    public short CosmeticId { get; set; }

    public int Color1 { get; set; }

    public int Color2 { get; set; }

    public int Color3 { get; set; }

    public int Color4 { get; set; }

    public int Color5 { get; set; }

    public int BonesId { get; set; } = 1;

    public int SkinId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public Account Account { get; set; } = null!;

    public Breed Breed { get; set; } = null!;

    public GameServer GameServer { get; set; } = null!;

    public CharacterStats Stats { get; set; } = null!;

    public CharacterPosition Position { get; set; } = null!;
}
