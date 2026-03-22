namespace Dofus210.Bll.Models;

public sealed record ServerBootstrapSnapshot(
    int PersistedAccountCount,
    int HardcodedAccountSeedCount);

