namespace Dofus210.Host.Auth;

public static class DofusMessageIds
{
    public const ushort ProtocolRequired = 1;
    public const ushort HelloConnect = 2607;
    public const ushort Identification = 4;
    public const ushort LoginQueueStatus = 10;
    public const ushort IdentificationFailed = 20;
    public const ushort IdentificationSuccess = 22;
    public const ushort ServersList = 30;
    public const ushort ServerSelection = 40;
    public const ushort SelectedServerRefused = 41;
    public const ushort SelectedServerData = 42;
    public const ushort HelloGame = 101;
    public const ushort AuthenticationTicket = 110;
    public const ushort AuthenticationTicketAccepted = 111;
    public const ushort AuthenticationTicketRefused = 112;
    public const ushort CharactersListRequest = 150;
    public const ushort CharactersList = 151;
    public const ushort CharacterCreationResult = 1146;
    public const ushort BasicPing = 182;
    public const ushort BasicPong = 183;
    public const ushort ClientKey = 5607;
    public const ushort CredentialsAcknowledgement = 6314;
}
