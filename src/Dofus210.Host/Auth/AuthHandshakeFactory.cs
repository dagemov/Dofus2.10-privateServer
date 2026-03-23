using System.Security.Cryptography;
using Dofus210.Host.Options;
using Microsoft.Extensions.Options;

namespace Dofus210.Host.Auth;

public sealed class AuthHandshakeFactory : IAuthHandshakeFactory
{
    private readonly ServerOptions _serverOptions;

    public AuthHandshakeFactory(IOptions<ServerOptions> serverOptions)
    {
        _serverOptions = serverOptions.Value;
    }

    public AuthHandshakePayloads Create()
    {
        var rsa = RSA.Create(2048);
        var publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
        var salt = Convert.ToHexString(RandomNumberGenerator.GetBytes(GetValidatedSaltByteLength()));

        var protocolRequiredPacket = CreateProtocolRequiredPacket(_serverOptions.AuthRequiredVersion);

        var helloConnectPacket = CreateHelloConnectPacket(
            salt,
            publicKeyBytes);

        return new AuthHandshakePayloads(
            rsa,
            salt,
            publicKeyBytes,
            protocolRequiredPacket,
            helloConnectPacket);
    }

    private byte GetValidatedSaltByteLength()
    {
        if (_serverOptions.AuthHandshakeSaltLength < 16)
        {
            throw new InvalidOperationException("AuthHandshakeSaltLength must be at least 16 bytes to produce a 32-character salt.");
        }

        return (byte)_serverOptions.AuthHandshakeSaltLength;
    }

    private static byte[] CreateProtocolRequiredPacket(string requiredVersion)
    {
        using var writer = new DofusDataWriter();

        writer.WriteUtf(requiredVersion);

        return DofusPacketCodec.Encode(DofusMessageIds.ProtocolRequired, writer.ToArray());
    }

    private static byte[] CreateHelloConnectPacket(string salt, ReadOnlySpan<byte> publicKeyBytes)
    {
        using var writer = new DofusDataWriter();

        writer.WriteUtf(salt);
        writer.WriteVarInt(publicKeyBytes.Length);
        writer.WriteBytes(publicKeyBytes);

        return DofusPacketCodec.Encode(DofusMessageIds.HelloConnect, writer.ToArray());
    }
}
