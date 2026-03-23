namespace Dofus210.Data.Entities;

public sealed class CharacterPosition
{
    public long CharacterId { get; set; }

    public int MapId { get; set; }

    public short CellId { get; set; }

    public byte Direction { get; set; } = 2;

    public Character Character { get; set; } = null!;
}
