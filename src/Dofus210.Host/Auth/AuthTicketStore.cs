using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Dofus210.Bll.Models;

namespace Dofus210.Host.Auth;

public sealed class AuthTicketStore : IAuthTicketStore
{
    private readonly ConcurrentDictionary<string, AuthTicketSession> _sessions = new(StringComparer.Ordinal);

    public AuthTicketSession Issue(
        AuthenticatedAccount account,
        short gameServerId,
        int timeToLiveMinutes,
        byte[]? ticketCipherKey = null)
    {
        var ticket = Convert.ToBase64String(RandomNumberGenerator.GetBytes(12));
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
        if (ticketCipherKey is not { Length: 32 })
        {
            return Encoding.ASCII.GetBytes(ticket);
        }

        var ticketBytes = Encoding.UTF8.GetBytes(ticket);
        var payload = new byte[ticketBytes.Length + 1];
        payload[0] = checked((byte)ticketBytes.Length);
        Buffer.BlockCopy(ticketBytes, 0, payload, 1, ticketBytes.Length);

        using var aes = Aes.Create();
        aes.Key = ticketCipherKey;
        aes.IV = ticketCipherKey.Take(16).ToArray();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        return encryptor.TransformFinalBlock(payload, 0, payload.Length);
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
