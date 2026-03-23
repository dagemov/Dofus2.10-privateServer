using Dofus210.Bll.Models;

namespace Dofus210.Host.Auth;

public interface IAuthTicketStore
{
    AuthTicketSession Issue(AuthenticatedAccount account, int timeToLiveMinutes);

    bool TryConsume(string ticket, out AuthTicketSession session);

    bool TryConsumeSingleOutstanding(out AuthTicketSession session);
}
