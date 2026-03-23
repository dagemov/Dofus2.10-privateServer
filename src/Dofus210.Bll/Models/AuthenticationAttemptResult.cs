namespace Dofus210.Bll.Models;

public sealed record AuthenticationAttemptResult(
    AuthenticatedAccount? Account,
    bool UsernameExists,
    bool PasswordMatched);
