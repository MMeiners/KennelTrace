namespace KennelTrace.Infrastructure.Features.Animals.AnimalRecords;

public interface IAnimalReadService
{
    Task<IReadOnlyList<AnimalLookupRow>> LookupAnimalsAsync(string? searchText, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AnimalMoveLocationOption>> ListMoveLocationsAsync(CancellationToken cancellationToken = default);

    Task<AnimalDetailResult?> GetAnimalDetailAsync(int animalId, CancellationToken cancellationToken = default);
}
