using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Dofus210.Bll.Models;

namespace Dofus210.Host.Auth;

public sealed class AuthTicketStore : IAuthTicketStore
{
    private readonly ConcurrentDictionary<string, AuthTicketSession> _sessions = new(StringComparer.Ordinal);

    public AuthTicketSession Issue(AuthenticatedAccount account, short gameServerId, int timeToLiveMinutes)
    {
        var ticket = Convert.ToBase64String(RandomNumberGenerator.GetBytes(12));
        var issuedAtUtc = DateTimeOffset.UtcNow;
        var expiresAtUtc = issuedAtUtc.AddMinutes(timeToLiveMinutes);
        var session = new AuthTicketSession(
            ticket,
            Encoding.ASCII.GetBytes(ticket),
            gameServerId,
            account,
            issuedAtUtc,
            expiresAtUtc);

        _sessions[ticket] = session;

        return session;
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
