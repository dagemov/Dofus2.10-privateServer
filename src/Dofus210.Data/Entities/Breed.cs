namespace Dofus210.Data.Entities;

public sealed class Breed
{
    public byte Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string MaleLook { get; set; } = string.Empty;

    public string FemaleLook { get; set; } = string.Empty;

    public int MaleBonesId { get; set; }

    public int FemaleBonesId { get; set; }

    public bool IsPlayable { get; set; } = true;

    public ICollection<Character> Characters { get; set; } = [];
}
