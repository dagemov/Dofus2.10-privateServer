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

    public static (string Language, string Ticket) ReadAuthenticationTicket(ReadOnlySpan<byte> payload)
    {
        var reader = new DofusDataReader(payload);
        var language = reader.ReadUtf();
        var ticket = reader.ReadUtf();

        return (language, ticket);
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

    public static byte[] CreateAuthenticationTicketRefusedPacket()
    {
        return DofusPacketCodec.Encode(DofusMessageIds.AuthenticationTicketRefused, []);
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
        flags = SetFlag(flags, 0, false);
        flags = SetFlag(flags, 1, true);

        writer.WriteByte(flags);
        writer.WriteVarShort(server.Id);
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

    private static byte SetFlag(byte box, int bit, bool value)
    {
        if (value)
        {
            return (byte)(box | (1 << bit));
        }

        return (byte)(box & ~(1 << bit));
    }
}
