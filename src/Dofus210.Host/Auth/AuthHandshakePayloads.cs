using System.Security.Cryptography;

namespace Dofus210.Host.Auth;

public sealed class AuthHandshakePayloads : IDisposable
{
    public AuthHandshakePayloads(
        RSA rsa,
        string salt,
        byte[] keyBytes,
        byte[] protocolRequiredPacket,
        byte[] helloConnectPacket)
    {
        Rsa = rsa;
        Salt = salt;
        KeyBytes = keyBytes;
        ProtocolRequiredPacket = protocolRequiredPacket;
        HelloConnectPacket = helloConnectPacket;
    }

    public RSA Rsa { get; }

    public string Salt { get; }

    public byte[] KeyBytes { get; }

    public byte[] ProtocolRequiredPacket { get; }

    public byte[] HelloConnectPacket { get; }

    public void Dispose()
    {
        Rsa.Dispose();
    }
}
