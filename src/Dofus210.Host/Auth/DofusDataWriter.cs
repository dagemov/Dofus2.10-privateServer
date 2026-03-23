using System.Buffers.Binary;
using System.Text;

namespace Dofus210.Host.Auth;

public sealed class DofusDataWriter : IDisposable
{
    private readonly MemoryStream _stream = new();

    public void WriteUtf(string value)
    {
        var encodedValue = Encoding.UTF8.GetBytes(value);

        if (encodedValue.Length > ushort.MaxValue)
        {
            throw new InvalidOperationException("Dofus UTF payload exceeds the supported size.");
        }

        Span<byte> lengthBuffer = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16BigEndian(lengthBuffer, (ushort)encodedValue.Length);

        _stream.Write(lengthBuffer);
        _stream.Write(encodedValue);
    }

    public void WriteVarInt(int value)
    {
        if (value < 0)
        {
            throw new InvalidOperationException("Negative values are not supported by this Dofus VarInt writer.");
        }

        var currentValue = (uint)value;

        while (currentValue >= 0x80)
        {
            _stream.WriteByte((byte)((currentValue & 0x7F) | 0x80));
            currentValue >>= 7;
        }

        _stream.WriteByte((byte)currentValue);
    }

    public void WriteVarShort(int value)
    {
        WriteVarInt(value);
    }

    public void WriteByte(byte value)
    {
        _stream.WriteByte(value);
    }

    public void WriteBoolean(bool value)
    {
        _stream.WriteByte(value ? (byte)1 : (byte)0);
    }

    public void WriteShort(short value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(short)];
        BinaryPrimitives.WriteInt16BigEndian(buffer, value);
        _stream.Write(buffer);
    }

    public void WriteUnsignedShort(ushort value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
        _stream.Write(buffer);
    }

    public void WriteInt(int value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        _stream.Write(buffer);
    }

    public void WriteDouble(double value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(buffer, BitConverter.DoubleToInt64Bits(value));
        _stream.Write(buffer);
    }

    public void WriteBytes(ReadOnlySpan<byte> value)
    {
        _stream.Write(value);
    }

    public byte[] ToArray()
    {
        return _stream.ToArray();
    }

    public void Dispose()
    {
        _stream.Dispose();
    }
}
