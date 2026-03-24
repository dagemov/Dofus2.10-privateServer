using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Dofus210.Bll.Models;

namespace Dofus210.Host.Auth;

public sealed class AuthTicketStore : IAuthTicketStore
{
    private const string TicketAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    private readonly ConcurrentDictionary<string, AuthTicketSession> _sessions = new(StringComparer.Ordinal);

    public AuthTicketSession Issue(
        AuthenticatedAccount account,
        short gameServerId,
        int timeToLiveMinutes,
        byte[]? ticketCipherKey = null)
    {
        var ticket = CreateTicket();
        var issuedAtUtc = DateTimeOffset.UtcNow;
        var expiresAtUtc = issuedAtUtc.AddMinutes(timeToLiveMinutes);
        var session = new AuthTicketSession(
            ticket,
            CreateTicketPayload(ticket, ticketCipherKey),
            gameServerId,
            account,
            issuedAtUtc,
            expiresAtUtc);

        _sessions[ticket] = session;

        return session;
    }

    private static byte[] CreateTicketPayload(string ticket, byte[]? ticketCipherKey)
    {
        var payload = CreatePlainTicketPayload(ticket);

        if (ticketCipherKey is not { Length: 32 })
        {
            return payload;
        }

        using var aes = Aes.Create();
        aes.Key = ticketCipherKey;
        aes.IV = ticketCipherKey.Take(16).ToArray();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        return encryptor.TransformFinalBlock(payload, 0, payload.Length);
    }

    private static string CreateTicket()
    {
        Span<char> characters = stackalloc char[32];

        for (var index = 0; index < characters.Length; index++)
        {
            var alphabetIndex = RandomNumberGenerator.GetInt32(TicketAlphabet.Length);
            characters[index] = TicketAlphabet[alphabetIndex];
        }

        return new string(characters);
    }

    private static byte[] CreatePlainTicketPayload(string ticket)
    {
        var ticketBytes = Encoding.ASCII.GetBytes(ticket);
        var payload = new byte[ticketBytes.Length + 1];
        payload[0] = checked((byte)ticketBytes.Length);
        Buffer.BlockCopy(ticketBytes, 0, payload, 1, ticketBytes.Length);
        return payload;
    }

    public bool TryConsume(string ticket, out AuthTicketSession session)
    {
        session = default!;

        if (!_sessions.TryRemove(ticket, out var storedSession))
        {
            return false;
        }

        if (storedSession.ExpiresAtUtc < DateTimeOffset.UtcNow)
        {
            return false;
        }

        session = storedSession;
        return true;
    }

    public bool TryConsumePayload(byte[] ticketPayload, out AuthTicketSession session)
    {
        session = default!;

        foreach (var pair in _sessions)
        {
            if (!pair.Value.TicketPayload.AsSpan().SequenceEqual(ticketPayload))
            {
                continue;
            }

            if (!TryConsume(pair.Key, out session))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    public bool TryConsumeSingleOutstanding(out AuthTicketSession session)
    {
        session = default!;

        if (_sessions.Count != 1)
        {
            return false;
        }

        var pair = _sessions.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(pair.Key))
        {
            return false;
        }

        return TryConsume(pair.Key, out session);
    }
}
