namespace Dofus210.Helper.Constants;

public static class ServerCatalogConstants
{
    // Core identity
    public const short ServerId = 1;
    public const string ServerName = "Aloha";

    // Network
    public const string Address = "127.0.0.1";
    public const int GamePort = 5556;

    // Catalog alignment (CRÍTICO para cliente)
    public const byte CommunityId = 0;   // <- importante para visibilidad
    public const byte Type = 0;
    // Client-side evidence:
    // ServerStatusEnum.STATUS_UNKNOWN = 0
    // ServerStatusEnum.OFFLINE      = 1
    // ServerStatusEnum.STARTING     = 2
    // ServerStatusEnum.ONLINE       = 3
    public const byte Status = 3;
    public const byte Completion = 0;

    // Gameplay limits
    public const byte CharacterSlots = 10;
    public const bool CanCreateNewCharacter = true;

    // Ticket
    public const int TicketTtlMinutes = 5;
}
