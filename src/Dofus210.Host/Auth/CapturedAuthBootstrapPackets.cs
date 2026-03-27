using System.Text;

namespace Dofus210.Host.Auth;

public sealed record AuthBootstrapPacketDefinition(
    byte[] Bytes,
    ushort? MessageId,
    string Label,
    string Summary);

public sealed record AuthBootstrapProfileDefinition(
    string Name,
    string Description,
    IReadOnlyList<AuthBootstrapPacketDefinition> Packets,
    bool MarkBootstrapSent,
    bool ReturnAfterSend);

public static class CapturedAuthBootstrapPackets
{
    private sealed record CapturedBootstrapArtifacts(
        IReadOnlyList<AuthBootstrapPacketDefinition> Packets,
        byte[] EmbeddedStumpPatchSwfBytes,
        byte[] Packet1Payload,
        byte[] Packet3Payload,
        byte[] BasicPongPayload);

    private static readonly Lazy<CapturedBootstrapArtifacts> CapturedAnkalineArtifacts =
        new(BuildCapturedAnkalineArtifacts);

    public static AuthBootstrapProfileDefinition CreateProfile(
        string profile,
        AuthHandshakePayloads handshakePayloads)
    {
        if (string.Equals(profile, "CapturedAnkaline", StringComparison.OrdinalIgnoreCase))
        {
            return new AuthBootstrapProfileDefinition(
                "CapturedAnkaline",
                "Replay del bootstrap capturado: stream concatenado con parche SWF custom (6253/StumpPatch) y paquetes auxiliares previos a Identification.",
                CapturedAnkalineArtifacts.Value.Packets,
                MarkBootstrapSent: true,
                ReturnAfterSend: true);
        }

        if (string.Equals(profile, "ExplicitStumpPatch6253", StringComparison.OrdinalIgnoreCase))
        {
            return new AuthBootstrapProfileDefinition(
                "ExplicitStumpPatch6253",
                "Bootstrap Alohafus reconstruido en backend: 6253 con contenedor explicito [ushort big-endian length][StumpPatch SWF], luego paquetes 1, 3 y 183.",
                BuildExplicitStumpPatchPackets(CapturedAnkalineArtifacts.Value),
                MarkBootstrapSent: true,
                ReturnAfterSend: true);
        }

        if (string.Equals(profile, "MinimalControlled", StringComparison.OrdinalIgnoreCase))
        {
            return new AuthBootstrapProfileDefinition(
                "MinimalControlled",
                "Bootstrap minimo controlado sin blob capturado: ProtocolRequired + HelloConnect.",
                [
                    new AuthBootstrapPacketDefinition(
                        handshakePayloads.ProtocolRequiredPacket,
                        DofusMessageIds.ProtocolRequired,
                        "ProtocolRequired",
                        "auth-version-check"),
                    new AuthBootstrapPacketDefinition(
                        handshakePayloads.HelloConnectPacket,
                        DofusMessageIds.HelloConnect,
                        "HelloConnect",
                        $"salt={handshakePayloads.Salt} publicKeyBytes={handshakePayloads.KeyBytes.Length}")
                ],
                MarkBootstrapSent: true,
                ReturnAfterSend: false);
        }

        if (string.IsNullOrWhiteSpace(profile) ||
            string.Equals(profile, "NoCaptured", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(profile, "None", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(profile, "Disabled", StringComparison.OrdinalIgnoreCase))
        {
            return new AuthBootstrapProfileDefinition(
                "NoCaptured",
                "Sin bootstrap capturado ni bootstrap forzado. El flujo queda en el handshake auth normal impulsado por el cliente.",
                [],
                MarkBootstrapSent: false,
                ReturnAfterSend: false);
        }

        return new AuthBootstrapProfileDefinition(
            profile,
            "Perfil no reconocido. Se omite bootstrap especial y se usa el handshake auth normal.",
            [],
            MarkBootstrapSent: false,
            ReturnAfterSend: false);
    }

    private static CapturedBootstrapArtifacts BuildCapturedAnkalineArtifacts()
    {
        var rawBlob = Convert.FromHexString(
            "61B70008E508E34357530A050F0000780195174B53DBDAF91C5BF2B16C8C6D2010480826E18640C0362637B9384062B0054E084981903417EA0AFB082BE8E191641E9D3B7DB0E97475BB6A779D3B9D4EBBEFAED3457FC09DCED045BBE9A6D355775577DDA5DF393236D0DCCED433FE74BEF7A7EF25E918097F47A8FBD7085DC7A8981C4408FDA0F76B8CD0BC5D53F31B4539756CE8A693076C61BCEEBA8D7C26737474943E9A4D5BF67E66666E6E2E93CD6572B9699098764E4C57399E369D3BE38BDC40913A555B6BB89A65A6984165CF6ABA0BE3E32DABB56ADB68A369EBDC64AD9AA13A35A8E93A9999F40C18AA55F3AA651B8ABBA8341ABA565598B9CCF1B453B7AA0747CA219D5675C5A9CF673A824CC7D55C9D2E166AD61E4DC93A3D4E3D48153AFA5CDA1761C2B54EA08B176E5361DAE9AA65641AB6556B562126154C71E58B2ACC44A3B9A76B4E9DDA8B4DF3C0B48E4C2ED5A13299AA4D15D7BA2C714E637C5D31F79BCA3E5D2CAD73ED36CE63545CBA389B4D198AEDA472D999593F0C469DCFB0EC5EC8768B02055C442389E6E0D8CCD8B63B567EBEBCB6F2D679B765BC3A9E7BB577401BF47B7B33E82C5EF8EB4F23BFFAF28B9FFDF6C9BFFF2865D71F1613FF08CEA3E5C0870F1FDE4582D01421F80BE8E523B8C0EF8B1FFDE2F70BD0245F4736DDA6D178A5B8D53AFA43DFCFBB10022ABA404CA0340A78BF14103AB828FB7439BFF3DAA1B6B3B36435EBB99DF32A4119534B4D4DAF517BA76365C7B1AB8F1F77F0B4E27475B0B16C94D73FDD7435DD91964E5C5AB06DE5245AA91436672B95F421AD86B66915F21ED44C37E10BAB9A4E9D13C7A58620C331C2C0A60BE5317AF7A95BA4AA666AACCF964ED615838E420BA415F34031947D409D74CD529B4E3ECF632D9BAAE5CC5C95784F6DE54033A91F56DAF795CF17347BB3AA9826B5F35735B8CDB449DD23CB3E4853B3698087655D8351289B8EABE8FAD6498396807EEFAA6ADB594D71957CFEADA12F5BA6AAEDAF5E15F47DE8D6BE5685B68630AAEC26D3866242D7D9E0AFD074EBE050535B73F6C2E74421293C08B34AE39DDC4325C6B2A3AD81E90C0BE43D93CB661F66F6A096AE66F6F939AF694E43574EF29B0D5B73E9D86562D167BEDC7B0F3141F4AE02C9B3472E0B954D17D20A311F525FF0C665FE2523377D1E3D64BB245F6217C667CD4AED90AFFFE0E3093AA0B64975560BA8403B4DCEAA62D6746ACF7D5CE9A395DBA2D5BA6941C24F58E1AEB1CC9BDB8AAE41A120F1059EFE7043711C287A2DDC849930A1C1C2703DA476B996509AAEB5090BB1EA6E72D2FCFFF6ED428738D0F1B603D6F3F96DFF503A76A959A335711916A5135B7A5D5E2B56B64B1B9BE597EBA2A1BCB76CD1D04CCB263678521CDAA339FE2C68E67ECB44B7AFB451DA2E33ADA88FBE2A6C2DAF4AACCA35D69B89E5B572697DABB2B9B5512ABC28AFAFC45A84A5D7EBC5B552A8AE38D0FF9116B150DEE86A1DE5B5C2E66A2F1BB92B11878F58ABBCDE92C30DCBE103198619ADB121171A4DA7DEBD0727A770A868BAB2A7D36FE8F7F3CAC0E43AD0E7CE85D6CFE7CBB54BFDEE8BF4C270C0FCA4D9024E579BB60D3261988192E9DA27832CD08FAAC540A4D32F820359174BB60DF9A50CF65D788015359BEFA41389B3D2EEB11BB5A963E98714F6693DCC16D20BAB46C5371BE5AD926035A8198356706DA8C9BD89D4422A7C8EE148EC3C4B2C2F4EA4759B20435A47C29A0A70815D45ADC658DC6DB9285675CBA191CE268B7456D4B58F6E1FA9BD5E06BE615BF47472703E337DBEA9CBE3405A8D1A856720AF82A23B2158D7B0AA05B6A0430EDFC8D73A0B27D339C62E8D7EC85F2AF12B63DE757107C42EAD87E47F2D93FE4B7CB8077F07DDAC54F6AD8A6B55585C955AFBD150A953BDF1E4FF7E925D78B0C1FA0C425F0773B9DCC0B9932BF683338F667B45DC2FF693FE407F44EC1B0884AF5DC74378A87F6860E8FAD0E0D0D0D08DFEEFF42BC1380E08628884A548B42BD61D4F2470207889B0460221120C13411A0EE02011A344EC228198D48BA55B581AC1520A4BA358BA8D8930267D82A5BB581AC7D23D2C4D6069124BF7B13485A5692CA5B194C152164B3358CA81F0AC140E10E101113E25E187447824E5B1F4184BF3585A00EE22119E10E129119688B84C8422114AD20AD0CB44784E84174478498457847C8B900D423609D922E43521DB84BC216484901421A384DC26E42D21DF26E41D21CF09F99C901D226092AC10E106E9F92E49EE916495246BF0AA128017905D000114A081CFF7D5BAC6E07B5C922589BF9DE0002E1531F07110611C968267D915786F918291F8EF42FF0A7A91B3EC9BC93F67FF890514A178F4D4C01C06380C722870287218E2907018E650E2D0D78DF27317C5A91F6223C62E82D1CD6971868846825D4246D28B9E9591D7ED0173974C75CB0483630F84008B732CE0812C60098E05BD24C7921C13BC1E8EF5002687D1A9217ABD9C7093B3435E1FC7863946BC7E0AF7A48BAAA4464E8DB037D046A3A786E45D6FA35DA746C41B6CA3B15323EADDE496EE80A5DD6EB5FBC74BE0ACCB1B06733EC46A5C4D78B70067A7A437D23AF578293805E45E74D843109AC6A709B87066DFA937CA0FD7BCDB2DE97EEF4E5B3ACAC582EAC0B94650BD7EEA8D71FE2058F3F9441D3AE713F5C6A97C333CE1DD8550F1338C58EABD71508002E8923C8C27BC7B2D2CCCB14910BCA5DEDA0DAAC14DBC84588DBCFB4C620C2923891E84242F0D28D4468FC929248F82852986A8B7C750DFDF3E7CF06678563EBB9895B89703111F4203E80214666A4EBE83BD59C0982D68113D3A3E2E8F9109EF019C43F2272C2371F92ED87F98F80D4267D93F65DFA1C9BF4C66EF43377C064E44555427A60AF22466CDE3CD0129F42C84780BE54119BA424FECDE57EFAB53F27460C27BCC6853320DE8584EA3AFA656BF9233607D9E939F31B29A6D531738758D5367DAD4454E5DE7D45C9BFA8451E55934311C0E784B2BF8FB3F11BF14C7EF251FB021FB948DE14304E3F808C1BBFF674844688E7D44E41154FD310AC3072692105A401184161194F009828F87A728865101750B6809C5115A46091115513284020198D4B9B32C8C6798A2DD157525BBBBAAAE6677CB6A39BBFB4C7D96DD7DAE3ECFEECAAACCC15B34CC7FF51268901584E2BD573E4C9EC29AF80F994C83CB00070000080000059400000594000F0000240020554B44594E5A454A44554F444E584853484B494443544C46524C58574B435453000002DF00000100");

        var packets = new List<AuthBootstrapPacketDefinition>();
        var offset = 0;
        while (offset < rawBlob.Length)
        {
            if (!DofusPacketCodec.TryGetPacketLength(rawBlob.AsSpan(offset), out var packetLength))
            {
                throw new InvalidOperationException($"Unable to split captured auth bootstrap at offset {offset}.");
            }

            var packetBytes = rawBlob.AsSpan(offset, packetLength).ToArray();

            if (!DofusPacketCodec.TryDecode(packetBytes, out var decodedPacket) || decodedPacket is null)
            {
                throw new InvalidOperationException($"Unable to decode captured auth bootstrap packet at offset {offset}.");
            }

            packets.Add(
                new AuthBootstrapPacketDefinition(
                    packetBytes,
                    decodedPacket.MessageId,
                    GetPacketLabel(decodedPacket.MessageId),
                    DescribePacketPayload(decodedPacket.MessageId, decodedPacket.Payload)));

            offset += packetLength;
        }

        if (packets.Count != 4)
        {
            throw new InvalidOperationException($"Expected 4 captured auth bootstrap packets but found {packets.Count}.");
        }

        if (!DofusPacketCodec.TryDecode(packets[0].Bytes, out var packet6253) || packet6253 is null)
        {
            throw new InvalidOperationException("Unable to decode captured 6253/StumpPatch packet.");
        }

        if (packet6253.Payload.Length < 2)
        {
            throw new InvalidOperationException("Captured 6253/StumpPatch payload is too short.");
        }

        if (!DofusPacketCodec.TryDecode(packets[1].Bytes, out var packet1) || packet1 is null ||
            !DofusPacketCodec.TryDecode(packets[2].Bytes, out var packet3) || packet3 is null ||
            !DofusPacketCodec.TryDecode(packets[3].Bytes, out var packet183) || packet183 is null)
        {
            throw new InvalidOperationException("Unable to decode captured auxiliary auth bootstrap packets.");
        }

        return new CapturedBootstrapArtifacts(
            packets,
            packet6253.Payload[2..].ToArray(),
            packet1.Payload.ToArray(),
            packet3.Payload.ToArray(),
            packet183.Payload.ToArray());
    }

    private static IReadOnlyList<AuthBootstrapPacketDefinition> BuildExplicitStumpPatchPackets(CapturedBootstrapArtifacts artifacts)
    {
        var stumpPatchPayload = BuildExplicitStumpPatchPayload(artifacts.EmbeddedStumpPatchSwfBytes);

        return
        [
            new AuthBootstrapPacketDefinition(
                DofusPacketCodec.Encode(6253, stumpPatchPayload),
                6253,
                "ExplicitStumpPatch6253",
                DescribePacketPayload(6253, stumpPatchPayload)),
            new AuthBootstrapPacketDefinition(
                artifacts.Packets[1].Bytes,
                1,
                "ExplicitPacketId1",
                DescribePacketPayload(1, artifacts.Packet1Payload)),
            new AuthBootstrapPacketDefinition(
                artifacts.Packets[2].Bytes,
                3,
                "ExplicitPacketId3",
                DescribePacketPayload(3, artifacts.Packet3Payload)),
            new AuthBootstrapPacketDefinition(
                artifacts.Packets[3].Bytes,
                183,
                "ExplicitBasicPong",
                DescribePacketPayload(183, artifacts.BasicPongPayload))
        ];
    }

    private static byte[] BuildExplicitStumpPatchPayload(byte[] embeddedSwfBytes)
    {
        var payload = new byte[embeddedSwfBytes.Length + 2];
        payload[0] = (byte)(embeddedSwfBytes.Length >> 8);
        payload[1] = (byte)embeddedSwfBytes.Length;
        Buffer.BlockCopy(embeddedSwfBytes, 0, payload, 2, embeddedSwfBytes.Length);
        return payload;
    }

    private static string GetPacketLabel(ushort messageId)
    {
        return messageId switch
        {
            6253 => "CapturedStumpPatch",
            183 => "CapturedBasicPong",
            1 => "CapturedPacketId1",
            3 => "CapturedPacketId3",
            _ => $"CapturedPacketId{messageId}"
        };
    }

    private static string DescribePacketPayload(ushort messageId, byte[] payload)
    {
        var builder = new StringBuilder()
            .Append($"messageId={messageId}")
            .Append($" payloadLength={payload.Length}");

        switch (messageId)
        {
            case 6253:
                builder.Append(DescribeStumpPatchPayload(payload));
                break;

            case 1:
            case 3:
            case 183:
                builder.Append($" payloadHex={Convert.ToHexString(payload)}");
                break;
        }

        var printableAscii = ToPrintableAscii(payload);
        if (!string.IsNullOrWhiteSpace(printableAscii))
        {
            builder.Append($" ascii={printableAscii}");
        }

        return builder.ToString();
    }

    private static string DescribeStumpPatchPayload(byte[] payload)
    {
        var builder = new StringBuilder();
        var cwsOffset = FindAsciiSequence(payload, "CWS");

        if (payload.Length >= 2)
        {
            var embeddedLength = ReadUInt16BigEndian(payload);
            var embeddedPayloadBytes = payload.Length - 2;
            builder.Append($" embeddedLengthBe={embeddedLength}");
            builder.Append($" embeddedPayloadBytes={embeddedPayloadBytes}");
            builder.Append($" embeddedLengthMatches={embeddedLength == embeddedPayloadBytes}");
        }

        if (cwsOffset >= 0)
        {
            builder.Append($" embeddedCwsOffset={cwsOffset}");
            builder.Append($" embeddedSwfType={(cwsOffset == 2 ? "cws-after-be-length" : "cws-nonstandard-offset")}");

            if (cwsOffset == 2)
            {
                builder.Append(" embeddedSwfNameHint=StumpPatch");
            }
        }

        if (TryReadVarInt(payload, out var prefixedLength, out var prefixBytes))
        {
            builder.Append($" rawVarIntPrefix={prefixedLength}");
            builder.Append($" rawVarIntBytes={prefixBytes}");
            builder.Append($" rawRemaining={payload.Length - prefixBytes}");
            builder.Append($" rawVarIntInterpretationMatches={prefixedLength == payload.Length - prefixBytes}");
        }
        else
        {
            builder.Append(" rawVarIntPrefix=unreadable");
        }

        return builder.ToString();
    }

    private static ushort ReadUInt16BigEndian(byte[] payload)
    {
        return (ushort)((payload[0] << 8) | payload[1]);
    }

    private static int FindAsciiSequence(byte[] buffer, string asciiSequence)
    {
        var needle = Encoding.ASCII.GetBytes(asciiSequence);

        for (var index = 0; index <= buffer.Length - needle.Length; index++)
        {
            if (buffer.AsSpan(index, needle.Length).SequenceEqual(needle))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool TryReadVarInt(byte[] payload, out int value, out int bytesConsumed)
    {
        value = 0;
        bytesConsumed = 0;
        var shift = 0;

        while (bytesConsumed < payload.Length)
        {
            var current = payload[bytesConsumed++];
            value |= (current & 0x7F) << shift;

            if ((current & 0x80) == 0)
            {
                return true;
            }

            shift += 7;

            if (shift > 35)
            {
                return false;
            }
        }

        return false;
    }

    private static string ToPrintableAscii(byte[] payload)
    {
        var value = Encoding.ASCII.GetString(payload);
        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            builder.Append(character is >= ' ' and <= '~' ? character : '.');
        }

        return builder
            .ToString()
            .Trim('.')
            .Trim();
    }
}
