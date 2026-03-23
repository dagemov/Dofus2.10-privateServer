namespace Dofus210.Data.Context;

public interface IAppDataContextInitializer
{
    Task<bool> EnsureCreatedAsync(CancellationToken cancellationToken = default);
}

