using System.Buffers.Binary;
using System.Text;

namespace Dofus210.Host.Auth;

public sealed class DofusDataReader
{
    private readonly byte[] _buffer;
    private int _offset;

    public DofusDataReader(ReadOnlySpan<byte> buffer)
    {
        _buffer = buffer.ToArray();
    }

    public int Remaining => _buffer.Length - _offset;

    public byte ReadByte()
    {
        EnsureAvailable(sizeof(byte));
        return _buffer[_offset++];
    }

    public bool ReadBoolean()
    {
        return ReadByte() != 0;
    }

    public short ReadShort()
    {
        EnsureAvailable(sizeof(short));
        var value = BinaryPrimitives.ReadInt16BigEndian(_buffer.AsSpan(_offset, sizeof(short)));
        _offset += sizeof(short);
        return value;
    }

    public ushort ReadUnsignedShort()
    {
        EnsureAvailable(sizeof(ushort));
        var value = BinaryPrimitives.ReadUInt16BigEndian(_buffer.AsSpan(_offset, sizeof(ushort)));
        _offset += sizeof(ushort);
        return value;
    }

    public int ReadInt()
    {
        EnsureAvailable(sizeof(int));
        var value = BinaryPrimitives.ReadInt32BigEndian(_buffer.AsSpan(_offset, sizeof(int)));
        _offset += sizeof(int);
        return value;
    }

    public string ReadUtf()
    {
        var byteLength = ReadUnsignedShort();

        if (byteLength == 0)
        {
            return string.Empty;
        }

        EnsureAvailable(byteLength);
        var value = Encoding.UTF8.GetString(_buffer, _offset, byteLength);
        _offset += byteLength;

        return value;
    }

    public byte[] ReadBytes(int count)
    {
        EnsureAvailable(count);
        var value = _buffer.AsSpan(_offset, count).ToArray();
        _offset += count;

        return value;
    }

    public byte[] ReadRemainingBytes()
    {
        return ReadBytes(Remaining);
    }

    public int ReadVarInt()
    {
        var result = 0;
        var shift = 0;

        while (true)
        {
            if (shift > 35)
            {
                throw new InvalidOperationException("VarInt is too large.");
            }

            var current = ReadByte();
            result |= (current & 0x7F) << shift;

            if ((current & 0x80) == 0)
            {
                return result;
            }

            shift += 7;
        }
    }

    private void EnsureAvailable(int count)
    {
        if (Remaining < count)
        {
            throw new InvalidOperationException("Unexpected end of Dofus payload.");
        }
    }
}
