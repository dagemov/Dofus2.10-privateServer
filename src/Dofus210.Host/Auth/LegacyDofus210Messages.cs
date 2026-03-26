using Dofus210.Bll.Models;
using Dofus210.Host.Options;
using System.Text;

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
    string Password,
    byte[] AesKey);

public sealed record LegacyAuthenticationTicketMessage(
    string Language,
    string? Ticket,
    byte[] RawTicketPayload,
    string EncodingMode);

public sealed record LegacyGameMapMovementRequest(
    IReadOnlyList<ushort> KeyMovements,
    long MapId);

public sealed record AuthTicketSession(
    string Ticket,
    byte[] TicketPayload,
    short GameServerId,
    AuthenticatedAccount Account,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc);

public static class LegacyDofus210Messages
{
    private const ushort CharacterBaseInformationsTypeId = 274;
    private const ushort GameRolePlayCharacterInformationsTypeId = 2594;
    private const ushort HumanInformationsTypeId = 6710;
    private const ushort EntityDispositionInformationsTypeId = 7343;
    private const short DefaultActionPoints = 6;
    private const short DefaultMovementPoints = 3;
    private const short DefaultEnergyPoints = 10_000;
    private const short DefaultProspecting = 100;

    public static byte[] CreateProtocolRequiredPacket(string requiredVersion)
    {
        using var writer = new DofusDataWriter();
        writer.WriteUtf(requiredVersion);

        return DofusPacketCodec.Encode(DofusMessageIds.ProtocolRequired, writer.ToArray());
    }

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
            var aesKey = reader.Remaining > 0
                ? reader.ReadBytes(reader.Remaining)
                : [];

            credentials = new LegacyCredentialBlob(username, password, aesKey);
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
        if (payload.IsEmpty)
        {
            throw new InvalidOperationException("ServerSelection payload cannot be empty.");
        }

        var reader = new DofusDataReader(payload);
        var serverId = reader.ReadVarInt();

        if (reader.Remaining != 0)
        {
            throw new InvalidOperationException("Unexpected trailing bytes in ServerSelection payload.");
        }

        return checked((short)serverId);
    }

    public static bool TryReadAuthenticationTicket(
        ReadOnlySpan<byte> payload,
        out LegacyAuthenticationTicketMessage? message)
    {
        message = null;

        try
        {
            var reader = new DofusDataReader(payload);
            var language = reader.ReadUtf();

            try
            {
                var ticket = reader.ReadUtf();

                if (reader.Remaining == 0)
                {
                    var normalizedTicket = NormalizePrefixedTicketString(ticket, out var normalizedPayload);

                    message = new LegacyAuthenticationTicketMessage(
                        language,
                        normalizedTicket,
                        normalizedPayload,
                        "UtfString");

                    return true;
                }
            }
            catch
            {
                // Fall through to raw ticket compatibility parsing.
            }

            var rawReader = new DofusDataReader(payload);
            language = rawReader.ReadUtf();

            if (rawReader.Remaining == 0)
            {
                message = new LegacyAuthenticationTicketMessage(language, string.Empty, [], "Empty");
                return true;
            }

            var remainingBytes = rawReader.ReadRemainingBytes();
            var rawTicketPayload = TryReadLengthPrefixedTicketBytes(remainingBytes, out var prefixedTicketBytes)
                ? prefixedTicketBytes
                : remainingBytes;

            message = new LegacyAuthenticationTicketMessage(
                language,
                TryDecodeAsciiTicket(rawTicketPayload),
                rawTicketPayload,
                ReferenceEquals(rawTicketPayload, remainingBytes) ? "RawBytes" : "LengthPrefixedBytes");

            return true;
        }
        catch
        {
            message = null;
            return false;
        }
    }

    public static bool TryReadCharacterCreationRequest(
        ReadOnlySpan<byte> payload,
        out CharacterCreationRequest? request)
    {
        request = null;

        try
        {
            var reader = new DofusDataReader(payload);
            var name = reader.ReadUtf();
            var breedId = reader.ReadByte();
            var sex = reader.ReadBoolean();
            var colors = new int[5];

            for (var index = 0; index < colors.Length; index++)
            {
                colors[index] = reader.ReadInt();
            }

            var cosmeticId = (short)reader.ReadVarInt();

            request = new CharacterCreationRequest(name, breedId, sex, colors, cosmeticId);
            return true;
        }
        catch
        {
            request = null;
            return false;
        }
    }

    public static bool TryReadCharacterSelection(
        ReadOnlySpan<byte> payload,
        out long characterId)
    {
        characterId = 0;

        try
        {
            var reader = new DofusDataReader(payload);
            characterId = reader.ReadVarLong();

            if (reader.Remaining != 0 || characterId <= 0)
            {
                throw new InvalidOperationException("Unexpected payload for CharacterSelection.");
            }

            return true;
        }
        catch
        {
            characterId = 0;
            return false;
        }
    }

    public static bool TryReadMapInformationsRequest(
        ReadOnlySpan<byte> payload,
        out long mapId)
    {
        mapId = 0;

        try
        {
            var reader = new DofusDataReader(payload);
            mapId = (long)reader.ReadDouble();

            if (reader.Remaining != 0 || mapId < 0)
            {
                throw new InvalidOperationException("Unexpected payload for MapInformationsRequest.");
            }

            return true;
        }
        catch
        {
            mapId = 0;
            return false;
        }
    }

    public static bool TryReadGameContextReady(
        ReadOnlySpan<byte> payload,
        out long mapId)
    {
        return TryReadMapInformationsRequest(payload, out mapId);
    }

    public static bool TryReadGameMapMovementRequest(
        ReadOnlySpan<byte> payload,
        out LegacyGameMapMovementRequest? request)
    {
        request = null;

        try
        {
            var reader = new DofusDataReader(payload);
            var keyMovementsCount = reader.ReadUnsignedShort();

            if (keyMovementsCount is 0 or > 64)
            {
                throw new InvalidOperationException("Unexpected key movement count.");
            }

            var keyMovements = new ushort[keyMovementsCount];

            for (var index = 0; index < keyMovements.Length; index++)
            {
                keyMovements[index] = reader.ReadUnsignedShort();
            }

            var mapId = (long)reader.ReadDouble();

            if (reader.Remaining != 0 || mapId < 0)
            {
                throw new InvalidOperationException("Unexpected payload for GameMapMovementRequest.");
            }

            request = new LegacyGameMapMovementRequest(keyMovements, mapId);
            return true;
        }
        catch
        {
            request = null;
            return false;
        }
    }

    public static short DecodeCellId(ushort keyMovement)
    {
        return (short)(keyMovement & 0x0FFF);
    }

    public static byte DecodeDirection(ushort keyMovement, byte fallbackDirection)
    {
        var direction = (byte)((keyMovement >> 12) & 0x07);
        return direction is > 0 and <= 7 ? direction : fallbackDirection;
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
        flags = SetFlag(flags, 0, false);
        flags = SetFlag(flags, 1, false);
        flags = SetFlag(flags, 2, false);

        writer.WriteByte(flags);
        writer.WriteUtf(account.Username);
        writer.WriteUtf(string.IsNullOrWhiteSpace(account.Nickname) ? account.Username : account.Nickname);
        writer.WriteUtf(string.Empty);
        writer.WriteInt(account.Id);
        writer.WriteByte(communityId);
        writer.WriteDouble(0);
        writer.WriteDouble(new DateTimeOffset(account.CreatedAtUtc).ToUnixTimeMilliseconds());
        writer.WriteByte(0);

        return DofusPacketCodec.Encode(DofusMessageIds.IdentificationSuccess, writer.ToArray());
    }

    public static byte[] CreateServersListPacket(IReadOnlyCollection<GameServerSummary> servers)
    {
        using var writer = new DofusDataWriter();

        writer.WriteUnsignedShort((ushort)servers.Count);

        foreach (var server in servers)
        {
            WriteGameServerInformations(writer, server);
        }

        writer.WriteBoolean(servers.Any(server => server.CanCreateNewCharacter));

        return DofusPacketCodec.Encode(DofusMessageIds.ServersList, writer.ToArray());
    }

    public static string DescribeIdentificationSuccessPacket(byte[] packet)
    {
        return DescribePacket(
            packet,
            static (payload, reader, lines) =>
            {
                var flagsOffset = GetOffset(payload, reader);
                var flags = reader.ReadByte();
                lines.Add(
                    DescribeField(
                        payload,
                        flagsOffset,
                        GetOffset(payload, reader),
                        "flags",
                        $"hasRights={GetFlag(flags, 0)} hasForceRight={GetFlag(flags, 1)} wasAlreadyConnected={GetFlag(flags, 2)}"));

                ReadUtfField(payload, reader, lines, "login");
                ReadUtfField(payload, reader, lines, "accountTag.nickname");
                ReadUtfField(payload, reader, lines, "accountTag.tagNumber");
                ReadIntField(payload, reader, lines, "accountId");
                ReadByteField(payload, reader, lines, "communityId");
                ReadDoubleField(payload, reader, lines, "subscriptionEndDate");
                ReadDoubleField(payload, reader, lines, "accountCreation");
                ReadByteField(payload, reader, lines, "havenbagAvailableRoom");
            });
    }

    public static string DescribeServersListPacket(byte[] packet)
    {
        return DescribePacket(
            packet,
            static (payload, reader, lines) =>
            {
                var count = ReadUnsignedShortField(payload, reader, lines, "serversCount");

                for (var index = 0; index < count; index++)
                {
                    var label = $"servers[{index}]";
                    var flagsOffset = GetOffset(payload, reader);
                    var flags = reader.ReadByte();
                    var flag0 = GetFlag(flags, 0);
                    var flag1 = GetFlag(flags, 1);
                    lines.Add(
                        DescribeField(
                            payload,
                            flagsOffset,
                            GetOffset(payload, reader),
                            $"{label}.flags",
                            $"flag0={flag0} flag1={flag1} baseline(selectable={flag0},mono={flag1}) alt(selectable={flag1},mono={flag0})"));

                    ReadUnsignedShortField(payload, reader, lines, $"{label}.id");
                    ReadByteField(payload, reader, lines, $"{label}.type");
                    ReadByteField(payload, reader, lines, $"{label}.status");
                    ReadByteField(payload, reader, lines, $"{label}.completion");
                    ReadByteField(payload, reader, lines, $"{label}.charactersCount");
                    ReadByteField(payload, reader, lines, $"{label}.characterCapacity");
                    ReadDoubleField(payload, reader, lines, $"{label}.date");
                }

                ReadBooleanField(payload, reader, lines, "canCreateNewCharacter");
            });
    }

    public static byte[] CreateSelectedServerDataPacket(GameServerSummary server, AuthTicketSession ticketSession)
    {
        using var writer = new DofusDataWriter();

        writer.WriteVarShort(server.Id);
        writer.WriteUtf(server.Address);
        writer.WriteUnsignedShort(1);
        writer.WriteVarShort(server.Port);
        writer.WriteBoolean(server.CanCreateNewCharacter);
        writer.WriteVarInt(ticketSession.TicketPayload.Length);
        writer.WriteBytes(ticketSession.TicketPayload);

        return DofusPacketCodec.Encode(DofusMessageIds.SelectedServerData, writer.ToArray());
    }

    public static byte[] CreateServerStatusUpdatePacket(GameServerSummary server)
    {
        using var writer = new DofusDataWriter();

        WriteGameServerInformations(writer, server);

        return DofusPacketCodec.Encode(DofusMessageIds.ServerStatusUpdate, writer.ToArray());
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

    public static byte[] CreateBasicAckPacket(int sequence, ushort lastPacketId)
    {
        using var writer = new DofusDataWriter();
        writer.WriteVarInt(sequence);
        writer.WriteVarShort(lastPacketId);

        return DofusPacketCodec.Encode(DofusMessageIds.BasicAck, writer.ToArray());
    }

    public static byte[] CreateAuthenticationTicketRefusedPacket()
    {
        return DofusPacketCodec.Encode(DofusMessageIds.AuthenticationTicketRefused, []);
    }

    public static byte[] CreateServerSettingsPacket(
        string language,
        byte communityId,
        byte gameType,
        bool isMonoAccount,
        short arenaLeaveBanTime,
        int itemMaxLevel,
        bool hasFreeAutopilot)
    {
        using var writer = new DofusDataWriter();
        var flags = (byte)0;
        flags = SetFlag(flags, 0, isMonoAccount);
        flags = SetFlag(flags, 1, hasFreeAutopilot);

        writer.WriteByte(flags);
        writer.WriteUtf(language);
        writer.WriteByte(communityId);
        writer.WriteByte(gameType);
        writer.WriteVarShort(arenaLeaveBanTime);
        writer.WriteInt(itemMaxLevel);

        return DofusPacketCodec.Encode(DofusMessageIds.ServerSettings, writer.ToArray());
    }

    public static byte[] CreateServerOptionalFeaturesPacket(params ushort[] features)
    {
        using var writer = new DofusDataWriter();
        writer.WriteUnsignedShort((ushort)features.Length);

        foreach (var feature in features)
        {
            writer.WriteVarShort(feature);
        }

        return DofusPacketCodec.Encode(DofusMessageIds.ServerOptionalFeatures, writer.ToArray());
    }

    public static byte[] CreateAccountCapabilitiesPacket(
        int accountId,
        bool tutorialAvailable,
        byte status,
        bool canCreateNewCharacter)
    {
        using var writer = new DofusDataWriter();
        var flags = (byte)0;
        flags = SetFlag(flags, 0, tutorialAvailable);
        flags = SetFlag(flags, 1, canCreateNewCharacter);

        writer.WriteByte(flags);
        writer.WriteInt(accountId);
        writer.WriteByte(status);

        return DofusPacketCodec.Encode(DofusMessageIds.AccountCapabilities, writer.ToArray());
    }

    public static byte[] CreateServerSessionConstantsPacket()
    {
        using var writer = new DofusDataWriter();
        writer.WriteUnsignedShort(3);

        WriteIntegerServerSessionConstant(writer, 2, 7_200_000);
        WriteIntegerServerSessionConstant(writer, 6, 10);
        WriteIntegerServerSessionConstant(writer, 7, 2_000);

        return DofusPacketCodec.Encode(DofusMessageIds.ServerSessionConstants, writer.ToArray());
    }

    public static byte[] CreateTrustStatusPacket(bool trusted)
    {
        using var writer = new DofusDataWriter();
        writer.WriteBoolean(trusted);

        return DofusPacketCodec.Encode(DofusMessageIds.TrustStatus, writer.ToArray());
    }

    public static byte[] CreateCharactersListPacket(IReadOnlyCollection<CharacterSummary> characters)
    {
        using var writer = new DofusDataWriter();

        writer.WriteUnsignedShort((ushort)characters.Count);

        foreach (var character in characters)
        {
            writer.WriteUnsignedShort(CharacterBaseInformationsTypeId);
            WriteCharacterBaseInformations(writer, character);
        }

        writer.WriteBoolean(false);

        return DofusPacketCodec.Encode(DofusMessageIds.CharactersList, writer.ToArray());
    }

    public static byte[] CreateCharacterCreationResultPacket(byte resultCode)
    {
        using var writer = new DofusDataWriter();
        writer.WriteByte(resultCode);
        return DofusPacketCodec.Encode(DofusMessageIds.CharacterCreationResult, writer.ToArray());
    }

    public static byte[] CreateCharacterSelectedSuccessPacket(CharacterSelectionContext context)
    {
        using var writer = new DofusDataWriter();
        WriteCharacterBaseInformations(writer, context.Character);
        writer.WriteBoolean(false);

        return DofusPacketCodec.Encode(DofusMessageIds.CharacterSelectedSuccess, writer.ToArray());
    }

    public static byte[] CreateGameContextCreatePacket(byte context = 1)
    {
        using var writer = new DofusDataWriter();
        writer.WriteByte(context);

        return DofusPacketCodec.Encode(DofusMessageIds.GameContextCreate, writer.ToArray());
    }

    public static byte[] CreateCharacterStatsListPacket(CharacterSelectionContext context)
    {
        using var writer = new DofusDataWriter();
        var experienceNextLevelFloor = Math.Max(context.Experience + 1000, 1000);
        var lifePoints = Math.Max(context.LifePoints, 1);
        var maxLifePoints = Math.Max(context.MaxLifePoints, lifePoints);
        var vitality = Math.Max(maxLifePoints - 50, 0);
        var initiative = Math.Max(maxLifePoints * 2, 100);

        writer.WriteVarLong(context.Experience);
        writer.WriteVarLong(0);
        writer.WriteVarLong(experienceNextLevelFloor);
        writer.WriteVarLong(0);
        writer.WriteVarLong(context.Kamas);
        WriteActorExtendedAlignmentInformations(writer);
        writer.WriteVarShort(0);
        writer.WriteVarShort(0);
        writer.WriteVarShort(0);
        writer.WriteVarInt(lifePoints);
        writer.WriteVarInt(maxLifePoints);
        writer.WriteVarShort(DefaultEnergyPoints);
        writer.WriteVarShort(DefaultEnergyPoints);
        writer.WriteVarShort(DefaultActionPoints);
        writer.WriteVarShort(DefaultMovementPoints);
        WriteCharacterBaseCharacteristic(writer, initiative);
        WriteCharacterBaseCharacteristic(writer, DefaultProspecting);
        WriteCharacterBaseCharacteristic(writer, DefaultActionPoints);
        WriteCharacterBaseCharacteristic(writer, DefaultMovementPoints);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer, vitality);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        writer.WriteVarShort(0);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        WriteCharacterBaseCharacteristic(writer);
        writer.WriteUnsignedShort(0);
        writer.WriteInt(0);

        return DofusPacketCodec.Encode(DofusMessageIds.CharacterStatsList, writer.ToArray());
    }

    public static byte[] CreateCurrentMapPacket(int mapId)
    {
        using var writer = new DofusDataWriter();
        writer.WriteDouble(mapId);

        return DofusPacketCodec.Encode(DofusMessageIds.CurrentMap, writer.ToArray());
    }

    public static byte[] CreateSetCharacterRestrictionsPacket(CharacterSelectionContext context)
    {
        using var writer = new DofusDataWriter();
        writer.WriteDouble(context.Character.Id);
        WriteActorRestrictionsInformations(writer);

        return DofusPacketCodec.Encode(DofusMessageIds.SetCharacterRestrictions, writer.ToArray());
    }

    public static byte[] CreateCharacterCapabilitiesPacket(int guildEmblemSymbolCategories = 0)
    {
        using var writer = new DofusDataWriter();
        writer.WriteVarInt(guildEmblemSymbolCategories);

        return DofusPacketCodec.Encode(DofusMessageIds.CharacterCapabilities, writer.ToArray());
    }

    public static byte[] CreateInventoryContentPacket(CharacterSelectionContext context)
    {
        using var writer = new DofusDataWriter();
        writer.WriteUnsignedShort(0);
        writer.WriteVarLong(context.Kamas);

        return DofusPacketCodec.Encode(DofusMessageIds.InventoryContent, writer.ToArray());
    }

    public static byte[] CreateInventoryWeightPacket(int inventoryWeight = 0, int shopWeight = 0, int weightMax = 1000)
    {
        using var writer = new DofusDataWriter();
        writer.WriteVarInt(inventoryWeight);
        writer.WriteVarInt(shopWeight);
        writer.WriteVarInt(weightMax);

        return DofusPacketCodec.Encode(DofusMessageIds.InventoryWeight, writer.ToArray());
    }

    public static byte[] CreateSpellListPacket(bool spellPrevisualization = false)
    {
        using var writer = new DofusDataWriter();
        writer.WriteBoolean(spellPrevisualization);
        writer.WriteUnsignedShort(0);

        return DofusPacketCodec.Encode(DofusMessageIds.SpellList, writer.ToArray());
    }

    public static byte[] CreateShortcutBarContentPacket(byte barType)
    {
        using var writer = new DofusDataWriter();
        writer.WriteByte(barType);
        writer.WriteUnsignedShort(0);

        return DofusPacketCodec.Encode(DofusMessageIds.ShortcutBarContent, writer.ToArray());
    }

    public static byte[] CreateEmoteListPacket()
    {
        using var writer = new DofusDataWriter();
        writer.WriteUnsignedShort(0);

        return DofusPacketCodec.Encode(DofusMessageIds.EmoteList, writer.ToArray());
    }

    public static byte[] CreateLifePointsRegenEndPacket(CharacterSelectionContext context)
    {
        using var writer = new DofusDataWriter();
        writer.WriteVarInt(context.LifePoints);
        writer.WriteVarInt(context.MaxLifePoints);
        writer.WriteVarInt(0);

        return DofusPacketCodec.Encode(DofusMessageIds.LifePointsRegenEnd, writer.ToArray());
    }

    public static byte[] CreatePlayerLifeStatusPacket(byte state = 0, long phenixMapId = 0)
    {
        using var writer = new DofusDataWriter();
        writer.WriteByte(state);
        writer.WriteDouble(phenixMapId);

        return DofusPacketCodec.Encode(DofusMessageIds.GameRolePlayPlayerLifeStatus, writer.ToArray());
    }

    public static byte[] CreateBasicDatePacket(DateTimeOffset timestamp)
    {
        using var writer = new DofusDataWriter();
        writer.WriteByte((byte)timestamp.Day);
        writer.WriteByte((byte)timestamp.Month);
        writer.WriteShort((short)timestamp.Year);

        return DofusPacketCodec.Encode(DofusMessageIds.BasicDate, writer.ToArray());
    }

    public static byte[] CreateBasicTimePacket(DateTimeOffset timestamp)
    {
        using var writer = new DofusDataWriter();
        writer.WriteDouble(timestamp.ToUnixTimeMilliseconds());
        writer.WriteShort((short)timestamp.Offset.TotalMinutes);

        return DofusPacketCodec.Encode(DofusMessageIds.BasicTime, writer.ToArray());
    }

    public static byte[] CreateBasicNoOperationPacket()
    {
        return DofusPacketCodec.Encode(DofusMessageIds.BasicNoOperation, []);
    }

    public static byte[] CreateMapComplementaryInformationsDataPacket(
        CharacterSelectionContext context,
        int accountId)
    {
        using var writer = new DofusDataWriter();

        writer.WriteVarShort(context.SubAreaId);
        writer.WriteDouble(context.MapId);
        writer.WriteUnsignedShort(0);
        writer.WriteUnsignedShort(1);
        writer.WriteUnsignedShort(GameRolePlayCharacterInformationsTypeId);
        WriteGameRolePlayCharacterInformations(writer, context, accountId);
        writer.WriteUnsignedShort(0);
        writer.WriteUnsignedShort(0);
        writer.WriteUnsignedShort(0);
        writer.WriteUnsignedShort(0);
        writer.WriteBoolean(false);
        writer.WriteUnsignedShort(0);
        writer.WriteUnsignedShort(0);

        return DofusPacketCodec.Encode(DofusMessageIds.MapComplementaryInformationsData, writer.ToArray());
    }

    public static byte[] CreateMapFightCountPacket(ushort fightCount = 0)
    {
        using var writer = new DofusDataWriter();
        writer.WriteVarShort(fightCount);

        return DofusPacketCodec.Encode(DofusMessageIds.MapFightCount, writer.ToArray());
    }

    private static void WriteGameServerInformations(DofusDataWriter writer, GameServerSummary server)
    {
        var flags = (byte)0;

        // bit 0 = selectable
        flags = SetFlag(flags, 0, true);

        // bit 1 = mono-account
        flags = SetFlag(flags, 1, false);

        writer.WriteByte(flags);
        writer.WriteUnsignedShort((ushort)server.Id);
        writer.WriteByte(server.Type);
        writer.WriteByte(server.Status);
        writer.WriteByte(server.Completion);
        writer.WriteByte(server.CharactersCount);
        writer.WriteByte(server.CharacterCapacity);
        writer.WriteDouble(0);
    }

    private static void WriteCharacterBaseInformations(DofusDataWriter writer, CharacterSummary character)
    {
        writer.WriteVarLong(character.Id);
        writer.WriteUtf(character.Name);
        writer.WriteVarShort(character.Level);
        WriteEntityLook(writer, character);
        writer.WriteByte(character.BreedId);
        writer.WriteBoolean(character.Sex);
    }

    private static void WriteEntityLook(DofusDataWriter writer, CharacterSummary character)
    {
        writer.WriteVarShort(character.BonesId);

        if (character.SkinId > 0)
        {
            writer.WriteUnsignedShort(1);
            writer.WriteVarShort(character.SkinId);
        }
        else
        {
            writer.WriteUnsignedShort(0);
        }

        writer.WriteUnsignedShort((ushort)character.IndexedColors.Count);

        foreach (var indexedColor in character.IndexedColors)
        {
            writer.WriteInt(indexedColor);
        }

        writer.WriteUnsignedShort(1);
        writer.WriteVarShort(character.ScalePercent);
        writer.WriteUnsignedShort(0);
    }

    private static void WriteActorExtendedAlignmentInformations(DofusDataWriter writer)
    {
        WriteActorAlignmentInformations(writer);
        writer.WriteVarShort(0);
        writer.WriteVarShort(0);
        writer.WriteVarShort(0);
        writer.WriteByte(0);
    }

    private static void WriteCharacterBaseCharacteristic(
        DofusDataWriter writer,
        int baseValue = 0,
        int additionalValue = 0,
        int objectsAndMountBonus = 0,
        int alignGiftBonus = 0,
        int contextModif = 0)
    {
        writer.WriteVarShort(baseValue);
        writer.WriteVarShort(additionalValue);
        writer.WriteVarShort(objectsAndMountBonus);
        writer.WriteVarShort(alignGiftBonus);
        writer.WriteVarShort(contextModif);
    }

    private static void WriteGameRolePlayCharacterInformations(
        DofusDataWriter writer,
        CharacterSelectionContext context,
        int accountId)
    {
        writer.WriteDouble(context.Character.Id);
        writer.WriteUnsignedShort(EntityDispositionInformationsTypeId);
        writer.WriteShort(context.CellId);
        writer.WriteByte(context.Direction);
        WriteEntityLook(writer, context.Character);
        writer.WriteUtf(context.Character.Name);
        writer.WriteUnsignedShort(HumanInformationsTypeId);
        WriteHumanInformations(writer, context.Character.Sex);
        writer.WriteInt(accountId);
        WriteActorAlignmentInformations(writer);
    }

    private static void WriteHumanInformations(DofusDataWriter writer, bool sex)
    {
        WriteActorRestrictionsInformations(writer);
        writer.WriteBoolean(sex);
        writer.WriteUnsignedShort(0);
    }

    private static void WriteActorRestrictionsInformations(DofusDataWriter writer)
    {
        writer.WriteByte(0);
        writer.WriteByte(0);
        writer.WriteByte(0);
    }

    private static void WriteActorAlignmentInformations(DofusDataWriter writer)
    {
        writer.WriteByte(0);
        writer.WriteByte(0);
        writer.WriteByte(0);
        writer.WriteDouble(0);
    }

    private static void WriteIntegerServerSessionConstant(
        DofusDataWriter writer,
        short id,
        int value)
    {
        writer.WriteUnsignedShort(4796);
        writer.WriteVarShort(id);
        writer.WriteInt(value);
    }

    private static byte SetFlag(byte box, int bit, bool value)
    {
        if (value)
        {
            return (byte)(box | (1 << bit));
        }

        return (byte)(box & ~(1 << bit));
    }

    private static bool TryReadLengthPrefixedTicketBytes(
        ReadOnlySpan<byte> payload,
        out byte[] ticketBytes)
    {
        ticketBytes = [];

        try
        {
            var reader = new DofusDataReader(payload);
            var byteCount = reader.ReadVarInt();

            if (byteCount < 0 || reader.Remaining != byteCount)
            {
                return false;
            }

            ticketBytes = reader.ReadBytes(byteCount);
            return true;
        }
        catch
        {
            ticketBytes = [];
            return false;
        }
    }

    private static string? TryDecodeAsciiTicket(byte[] payload)
    {
        if (payload.Length == 0 || payload.Any(value => value is < 0x20 or > 0x7E))
        {
            return null;
        }

        return Encoding.ASCII.GetString(payload);
    }

    private static string NormalizePrefixedTicketString(string ticket, out byte[] rawPayload)
    {
        rawPayload = Encoding.ASCII.GetBytes(ticket);

        if (string.IsNullOrEmpty(ticket))
        {
            return ticket;
        }

        var prefixLength = (byte)ticket[0];

        if (prefixLength == ticket.Length - 1 &&
            ticket.AsSpan(1).ToArray().All(static character => character is >= '!' and <= '~'))
        {
            return ticket[1..];
        }

        return ticket;
    }

    private static string DescribePacket(
        byte[] packet,
        Action<byte[], DofusDataReader, List<string>> payloadDescriber)
    {
        if (!DofusPacketCodec.TryDecode(packet, out var decodedPacket) || decodedPacket is null)
        {
            return $"Unable to decode packet. Hex={Convert.ToHexString(packet)}";
        }

        var payload = decodedPacket.Payload;
        var reader = new DofusDataReader(payload);
        var lines = new List<string>
        {
            $"packetHex={Convert.ToHexString(packet)}",
            $"messageId={decodedPacket.MessageId}",
            $"payloadHex={Convert.ToHexString(payload)}"
        };

        payloadDescriber(payload, reader, lines);

        if (reader.Remaining > 0)
        {
            var trailingOffset = payload.Length - reader.Remaining;
            lines.Add(
                DescribeField(
                    payload,
                    trailingOffset,
                    payload.Length,
                    "trailingBytes",
                    $"{reader.Remaining} unread byte(s)"));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static int ReadUnsignedShortField(
        byte[] payload,
        DofusDataReader reader,
        List<string> lines,
        string name)
    {
        var start = GetOffset(payload, reader);
        var value = reader.ReadUnsignedShort();
        lines.Add(DescribeField(payload, start, GetOffset(payload, reader), name, value.ToString()));
        return value;
    }

    private static void ReadUtfField(
        byte[] payload,
        DofusDataReader reader,
        List<string> lines,
        string name)
    {
        var start = GetOffset(payload, reader);
        var value = reader.ReadUtf();
        lines.Add(DescribeField(payload, start, GetOffset(payload, reader), name, $"\"{value}\""));
    }

    private static void ReadIntField(
        byte[] payload,
        DofusDataReader reader,
        List<string> lines,
        string name)
    {
        var start = GetOffset(payload, reader);
        var value = reader.ReadInt();
        lines.Add(DescribeField(payload, start, GetOffset(payload, reader), name, value.ToString()));
    }

    private static void ReadByteField(
        byte[] payload,
        DofusDataReader reader,
        List<string> lines,
        string name)
    {
        var start = GetOffset(payload, reader);
        var value = reader.ReadByte();
        lines.Add(DescribeField(payload, start, GetOffset(payload, reader), name, value.ToString()));
    }

    private static void ReadBooleanField(
        byte[] payload,
        DofusDataReader reader,
        List<string> lines,
        string name)
    {
        var start = GetOffset(payload, reader);
        var value = reader.ReadBoolean();
        lines.Add(DescribeField(payload, start, GetOffset(payload, reader), name, value.ToString()));
    }

    private static void ReadDoubleField(
        byte[] payload,
        DofusDataReader reader,
        List<string> lines,
        string name)
    {
        var start = GetOffset(payload, reader);
        var value = reader.ReadDouble();
        lines.Add(DescribeField(payload, start, GetOffset(payload, reader), name, value.ToString("R")));
    }

    private static int GetOffset(byte[] payload, DofusDataReader reader)
    {
        return payload.Length - reader.Remaining;
    }

    private static bool GetFlag(byte value, int bit)
    {
        return (value & (1 << bit)) != 0;
    }

    private static string DescribeField(
        byte[] payload,
        int startInclusive,
        int endExclusive,
        string name,
        string value)
    {
        var length = Math.Max(0, endExclusive - startInclusive);
        var hex = length > 0
            ? Convert.ToHexString(payload.AsSpan(startInclusive, length))
            : string.Empty;

        return $"offset={startInclusive:D3} len={length:D2} field={name} hex={hex} value={value}";
    }
}
