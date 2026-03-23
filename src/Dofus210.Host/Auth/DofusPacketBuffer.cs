using System.Runtime.InteropServices;

namespace Dofus210.Host.Auth;

public sealed class DofusPacketBuffer
{
    private readonly List<byte> _buffer = [];

    public int Count => _buffer.Count;

    public void Append(ReadOnlySpan<byte> data)
    {
        foreach (var currentByte in data)
        {
            _buffer.Add(currentByte);
        }
    }

    public void Clear()
    {
        _buffer.Clear();
    }

    public bool TryReadPacket(out byte[] packetBytes)
    {
        packetBytes = Array.Empty<byte>();

        if (_buffer.Count == 0)
        {
            return false;
        }

        if (!DofusPacketCodec.TryGetPacketLength(CollectionsMarshal.AsSpan(_buffer), out var packetLength))
        {
            return false;
        }

        if (_buffer.Count < packetLength)
        {
            return false;
        }

        packetBytes = _buffer.GetRange(0, packetLength).ToArray();
        _buffer.RemoveRange(0, packetLength);

        return true;
    }
}
