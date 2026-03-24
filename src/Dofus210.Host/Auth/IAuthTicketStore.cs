using Dofus210.Bll.Models;

namespace Dofus210.Host.Auth;

public interface IAuthTicketStore
{
    AuthTicketSession Issue(AuthenticatedAccount account, short gameServerId, int timeToLiveMinutes, byte[]? ticketCipherKey = null);

    bool TryConsume(string ticket, out AuthTicketSession session);

    bool TryConsumePayload(byte[] ticketPayload, out AuthTicketSession session);

    bool TryConsumeSingleOutstanding(out AuthTicketSession session);
}
