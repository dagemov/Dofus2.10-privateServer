using Dofus210.Bll.Models;
using Dofus210.Host.Options;

namespace Dofus210.Host.Auth;

public sealed record LegacyVersion(
    byte Major,
    byte Minor,
    byte Release,
    int Revision,
    byte Patch,
    byte BuildType,
    byte InstallationType = 0,
    byte TechnologyType = 0);

public sealed record LegacyIdentificationMessage(
    bool AutoConnect,
    bool UseCertificate,
    bool UseLoginToken,
    LegacyVersion Version,
    string Language,
    byte[] Credentials,
    short ServerId);

public sealed record LegacyCredentialBlob(
    string Username,
    string Password);

public sealed record AuthTicketSession(
    string Ticket,
    byte[] TicketPayload,
    AuthenticatedAccount Account,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc);

public static class LegacyDofus210Messages
{
    public static bool TryReadIdentification(
        ReadOnlySpan<byte> payload,
        out LegacyIdentificationMessage? message)
    {
        message = null;

        return TryReadIdentificationExtended(payload, out message) ||
               TryReadIdentificationLegacy(payload, out message);
    }

    private static bool TryReadIdentificationExtended(
        ReadOnlySpan<byte> payload,
        out LegacyIdentificationMessage? message)
    {
        message = null;

        try
        {
            var reader = new DofusDataReader(payload);
            var flags = reader.ReadByte();
            var version = new LegacyVersion(
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadInt(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte());
            var language = reader.ReadUtf();
            var credentialsLength = reader.ReadUnsignedShort();
            var credentials = reader.ReadBytes(credentialsLength);
            var serverId = reader.ReadShort();

            if (reader.Remaining != 0)
            {
                throw new InvalidOperationException("Unexpected trailing bytes in extended IdentificationMessage.");
            }

            message = new LegacyIdentificationMessage(
                (flags & 0b0000_0001) != 0,
                (flags & 0b0000_0010) != 0,
                (flags & 0b0000_0100) != 0,
                version,
                language,
                credentials,
                serverId);

            return true;
        }
        catch
        {
            message = null;
            return false;
        }
    }

    private static bool TryReadIdentificationLegacy(
        ReadOnlySpan<byte> payload,
        out LegacyIdentificationMessage? message)
    {
        message = null;

        try
        {
            var reader = new DofusDataReader(payload);
            var flags = reader.ReadByte();
            var version = new LegacyVersion(
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadInt(),
                reader.ReadByte(),
                reader.ReadByte());
            var language = reader.ReadUtf();
            var credentialsLength = reader.ReadUnsignedShort();
            var credentials = reader.ReadBytes(credentialsLength);
            var serverId = reader.ReadShort();

            if (reader.Remaining != 0)
            {
                throw new InvalidOperationException("Unexpected trailing bytes in legacy IdentificationMessage.");
            }

            message = new LegacyIdentificationMessage(
                (flags & 0b0000_0001) != 0,
                (flags & 0b0000_0010) != 0,
                (flags & 0b0000_0100) != 0,
                version,
                language,
                credentials,
                serverId);

            return true;
        }
        catch
        {
            message = null;
            return false;
        }
    }

    public static bool TryReadCredentials(
        ReadOnlySpan<byte> payload,
        out LegacyCredentialBlob? credentials)
    {
        credentials = null;

        try
        {
            var reader = new DofusDataReader(payload);
            var username = reader.ReadUtf();
            var password = reader.ReadUtf();

            credentials = new LegacyCredentialBlob(username, password);
            return true;
        }
        catch
        {
            credentials = null;
            return false;
        }
    }

    public static string ReadClientKey(ReadOnlySpan<byte> payload)
    {
        var reader = new DofusDataReader(payload);
        return reader.ReadUtf();
    }

    public static short ReadServerSelection(ReadOnlySpan<byte> payload)
    {
        var reader = new DofusDataReader(payload);

        return payload.Length switch
        {
            1 => (short)reader.ReadByte(),
            2 => reader.ReadShort(),
            _ => (short)reader.ReadVarInt()
        };
    }

    public static (string Language, string Ticket) ReadAuthenticationTicket(ReadOnlySpan<byte> payload)
    {
        var reader = new DofusDataReader(payload);
        var language = reader.ReadUtf();
        var ticket = reader.ReadUtf();

        return (language, ticket);
    }

    public static byte[] CreateCredentialsAcknowledgementPacket()
    {
        return DofusPacketCodec.Encode(DofusMessageIds.CredentialsAcknowledgement, []);
    }

    public static byte[] CreateIdentificationFailedPacket(byte reason)
    {
        using var writer = new DofusDataWriter();
        writer.WriteByte(reason);

        return DofusPacketCodec.Encode(DofusMessageIds.IdentificationFailed, writer.ToArray());
    }

    public static byte[] CreateIdentificationSuccessPacket(
        AuthenticatedAccount account,
        byte communityId)
    {
        using var writer = new DofusDataWriter();
        var flags = (byte)0;
        flags = SetFlag(flags, 0, account.IsGameMaster);
        flags = SetFlag(flags, 1, false);

        writer.WriteByte(flags);
        writer.WriteUtf(account.Username);
        writer.WriteUtf(string.IsNullOrWhiteSpace(account.Nickname) ? account.Username : account.Nickname);
        writer.WriteInt(account.Id);
        writer.WriteByte(communityId);
        writer.WriteUtf(string.Empty);
        writer.WriteDouble(0);
        writer.WriteDouble(new DateTimeOffset(account.CreatedAtUtc).ToUnixTimeMilliseconds());

        return DofusPacketCodec.Encode(DofusMessageIds.IdentificationSuccess, writer.ToArray());
    }

    public static byte[] CreateServersListPacket(ServerOptions options)
    {
        using var writer = new DofusDataWriter();

        writer.WriteUnsignedShort(1);
        WriteGameServerInformations(writer, options);
        writer.WriteUnsignedShort(0);
        writer.WriteBoolean(options.GameServerCanCreateNewCharacter);

        return DofusPacketCodec.Encode(DofusMessageIds.ServersList, writer.ToArray());
    }

    public static byte[] CreateSelectedServerDataPacket(ServerOptions options, AuthTicketSession ticketSession)
    {
        using var writer = new DofusDataWriter();

        writer.WriteVarShort(options.GameServerId);
        writer.WriteUtf(options.GameServerAddress);
        writer.WriteUnsignedShort(1);
        writer.WriteVarShort(options.GamePort);
        writer.WriteBoolean(options.GameServerCanCreateNewCharacter);
        writer.WriteVarInt(ticketSession.TicketPayload.Length);
        writer.WriteBytes(ticketSession.TicketPayload);

        return DofusPacketCodec.Encode(DofusMessageIds.SelectedServerData, writer.ToArray());
    }

    public static byte[] CreateSelectedServerRefusedPacket(short serverId, byte error, byte status)
    {
        using var writer = new DofusDataWriter();

        writer.WriteVarShort(serverId);
        writer.WriteByte(error);
        writer.WriteByte(status);

        return DofusPacketCodec.Encode(DofusMessageIds.SelectedServerRefused, writer.ToArray());
    }

    public static byte[] CreateHelloGamePacket()
    {
        return DofusPacketCodec.Encode(DofusMessageIds.HelloGame, []);
    }

    public static byte[] CreateAuthenticationTicketAcceptedPacket()
    {
        return DofusPacketCodec.Encode(DofusMessageIds.AuthenticationTicketAccepted, []);
    }

    public static byte[] CreateAuthenticationTicketRefusedPacket()
    {
        return DofusPacketCodec.Encode(DofusMessageIds.AuthenticationTicketRefused, []);
    }

    public static byte[] CreateCharactersListPacket()
    {
        using var writer = new DofusDataWriter();

        writer.WriteUnsignedShort(0);
        writer.WriteBoolean(false);

        return DofusPacketCodec.Encode(DofusMessageIds.CharactersList, writer.ToArray());
    }

    private static void WriteGameServerInformations(DofusDataWriter writer, ServerOptions options)
    {
        var flags = (byte)0;
        flags = SetFlag(flags, 0, true);

        writer.WriteByte(flags);
        writer.WriteVarShort(options.GameServerId);
        writer.WriteByte(options.GameServerType);
        writer.WriteByte(options.GameServerStatus);
        writer.WriteByte(options.GameServerCompletion);
        writer.WriteByte(0);
        writer.WriteDouble(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    private static byte SetFlag(byte box, int bit, bool value)
    {
        if (value)
        {
            return (byte)(box | (1 << bit));
        }

        return (byte)(box & ~(1 << bit));
    }
}
