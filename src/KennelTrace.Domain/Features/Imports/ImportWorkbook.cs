namespace KennelTrace.Domain.Features.Imports;

public sealed record ImportWorkbook(
    IReadOnlyList<FacilityImportRow> Facilities,
    IReadOnlyList<RoomImportRow> Rooms,
    IReadOnlyList<KennelImportRow> Kennels,
    IReadOnlyList<LocationLinkImportRow> LocationLinks);
