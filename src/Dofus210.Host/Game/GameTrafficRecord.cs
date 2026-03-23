namespace Dofus210.Host.Game;

public sealed record GameTrafficRecord(
    DateTimeOffset TimestampUtc,
    string ConnectionId,
    string Direction,
    string RemoteEndPoint,
    int ByteCount,
    string HexPayload);
