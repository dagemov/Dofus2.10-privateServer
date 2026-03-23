using Dofus210.Bll.Models;

namespace Dofus210.Bll.Services;

public interface ICharacterDirectoryService
{
    Task<IReadOnlyList<CharacterSummary>> ListForAccountAsync(
        int accountId,
        short gameServerId,
        CancellationToken cancellationToken = default);

    Task<bool> CanCreateAsync(
        int accountId,
        short gameServerId,
        CancellationToken cancellationToken = default);

    Task<CharacterCreationResult> CreateAsync(
        int accountId,
        short gameServerId,
        CharacterCreationRequest request,
        CancellationToken cancellationToken = default);

    Task<CharacterSelectionContext?> GetSelectionContextAsync(
        int accountId,
        short gameServerId,
        long characterId,
        CancellationToken cancellationToken = default);

    Task<CharacterSelectionContext?> UpdatePositionAsync(
        int accountId,
        short gameServerId,
        long characterId,
        int mapId,
        short cellId,
        byte direction,
        CancellationToken cancellationToken = default);
}
