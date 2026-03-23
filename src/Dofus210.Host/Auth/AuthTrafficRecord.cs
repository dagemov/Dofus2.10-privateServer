namespace Dofus210.Host.Auth;

public sealed record AuthTrafficRecord(
    DateTimeOffset TimestampUtc,
    string ConnectionId,
    string Direction,
    string RemoteEndPoint,
    int ByteCount,
    string HexPayload,
    string AsciiPayload);

