namespace Dofus210.Host.Auth;

public sealed record DofusPacket(
    ushort MessageId,
    byte LengthBytesCount,
    int PayloadLength,
    byte[] Payload);

