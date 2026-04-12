using KennelTrace.Domain.Features.Locations;

namespace KennelTrace.Domain.Features.Imports;

public sealed record RoomImportRow(
    int RowNumber,
    string FacilityCode,
    string RoomCode,
    string RoomName,
    LocationType RoomType,
    string? ParentLocationCode,
    bool IsActive,
    int? DisplayOrder,
    string? SourceReference,
    string? Notes);
