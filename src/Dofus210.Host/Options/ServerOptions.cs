namespace Dofus210.Host.Options;

public sealed class ServerOptions
{
    public const string SectionName = "Server";

    public string Name { get; set; } = "Henual";

    public string Host { get; set; } = "127.0.0.1";

    public int AuthPort { get; set; } = 5555;

    public int GamePort { get; set; } = 5556;

    public bool EnableSocketPolicyServer { get; set; } = true;

    public int SocketPolicyPort { get; set; } = 843;

    public bool AutoMigrate { get; set; }

    public int AuthReceiveBufferSize { get; set; } = 4096;

    public int AuthReceiveTimeoutMs { get; set; } = 120000;

    public int GameReceiveTimeoutMs { get; set; } = 120000;

    public string AuthRequiredVersion { get; set; } = "2.10.0.65664.0";

    public string AuthCurrentVersion { get; set; } = "2.10.0.65664.0";

    public int AuthHandshakeSaltLength { get; set; } = 32;

    public string AuthBootstrapProfile { get; set; } = "CapturedAnkaline";

    public string AuthTranscriptDirectory { get; set; } = "..\\..\\runtime\\auth";

    public string GameTranscriptDirectory { get; set; } = "..\\..\\runtime\\game";

    public short GameServerId { get; set; } = 4001;

    public byte ServerCommunityId { get; set; } = 4;

    public string GameServerAddress { get; set; } = "127.0.0.1";

    public byte GameServerType { get; set; } = 0;

    public byte GameServerStatus { get; set; } = 3;

    public byte GameServerCompletion { get; set; } = 0;

    public byte GameServerCharacterSlots { get; set; } = 5;

    public bool GameServerCanCreateNewCharacter { get; set; } = true;

    public int GameTicketTimeToLiveMinutes { get; set; } = 5;

    public string GameApproachProfile { get; set; } = "GinyAckPushList";

    public bool GameSendProtocolRequiredOnConnect { get; set; }

    public bool GameSendHelloGameOnConnect { get; set; }
}
