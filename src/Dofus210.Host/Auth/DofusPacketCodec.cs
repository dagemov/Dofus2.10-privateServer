namespace Dofus210.Host.Auth;

public static class DofusPacketCodec
{
    public static bool TryGetPacketLength(ReadOnlySpan<byte> buffer, out int totalLength)
    {
        totalLength = 0;

        if (buffer.Length < 2)
        {
            return false;
        }

        var staticHeader = (ushort)((buffer[0] << 8) | buffer[1]);
        var lengthBytesCount = (byte)(staticHeader & 0b11);

        if (buffer.Length < 2 + lengthBytesCount)
        {
            return false;
        }

        var payloadLength = 0;

        for (var index = 0; index < lengthBytesCount; index++)
        {
            payloadLength = (payloadLength << 8) | buffer[2 + index];
        }

        totalLength = 2 + lengthBytesCount + payloadLength;
        return true;
    }

    public static bool TryDecode(ReadOnlySpan<byte> buffer, out DofusPacket? packet)
    {
        packet = null;

        if (!TryGetPacketLength(buffer, out var totalLength))
        {
            return false;
        }

        if (buffer.Length != totalLength)
        {
            return false;
        }

        var staticHeader = (ushort)((buffer[0] << 8) | buffer[1]);
        var lengthBytesCount = (byte)(staticHeader & 0b11);
        var messageId = (ushort)(staticHeader >> 2);
        var payloadLength = totalLength - 2 - lengthBytesCount;

        var payload = buffer.Slice(2 + lengthBytesCount, payloadLength).ToArray();
        packet = new DofusPacket(messageId, lengthBytesCount, payloadLength, payload);

        return true;
    }

    public static byte[] Encode(ushort messageId, ReadOnlySpan<byte> payload)
    {
        var lengthBytesCount = ComputeLengthBytesCount(payload.Length);
        var buffer = new byte[2 + lengthBytesCount + payload.Length];
        var staticHeader = (ushort)((messageId << 2) | lengthBytesCount);

        buffer[0] = (byte)(staticHeader >> 8);
        buffer[1] = (byte)(staticHeader & 0xFF);

        switch (lengthBytesCount)
        {
            case 0:
                break;

            case 1:
                buffer[2] = (byte)payload.Length;
                break;

            case 2:
                buffer[2] = (byte)(payload.Length >> 8);
                buffer[3] = (byte)(payload.Length & 0xFF);
                break;

            case 3:
                buffer[2] = (byte)((payload.Length >> 16) & 0xFF);
                buffer[3] = (byte)((payload.Length >> 8) & 0xFF);
                buffer[4] = (byte)(payload.Length & 0xFF);
                break;

            default:
                throw new InvalidOperationException("Invalid Dofus packet length encoding.");
        }

        payload.CopyTo(buffer.AsSpan(2 + lengthBytesCount));
        return buffer;
    }

    private static byte ComputeLengthBytesCount(int payloadLength)
    {
        if (payloadLength > 65535)
        {
            return 3;
        }

        if (payloadLength > 255)
        {
            return 2;
        }

        if (payloadLength > 0)
        {
            return 1;
        }

        return 0;
    }
}
