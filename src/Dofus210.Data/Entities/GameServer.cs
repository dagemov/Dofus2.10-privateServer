namespace Dofus210.Data.Entities;

public sealed class GameServer
{
    public short Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public int Port { get; set; }

    public byte CommunityId { get; set; }

    public byte Type { get; set; }

    public byte Status { get; set; }

    public byte Completion { get; set; }

    public byte CharacterCapacity { get; set; }

    public bool CanCreateNewCharacter { get; set; }

    public ICollection<Character> Characters { get; set; } = [];
}
