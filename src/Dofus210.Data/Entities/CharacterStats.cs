namespace Dofus210.Data.Entities;

public sealed class CharacterStats
{
    public long CharacterId { get; set; }

    public long Kamas { get; set; }

    public short StatsPoints { get; set; }

    public short SpellsPoints { get; set; }

    public int LifePoints { get; set; }

    public int MaxLifePoints { get; set; }

    public short EnergyPoints { get; set; }

    public short MaxEnergyPoints { get; set; }

    public short ActionPoints { get; set; } = 6;

    public short MovementPoints { get; set; } = 3;

    public Character Character { get; set; } = null!;
}
