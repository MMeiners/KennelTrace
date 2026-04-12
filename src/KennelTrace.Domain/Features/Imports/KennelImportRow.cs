namespace KennelTrace.Domain.Features.Imports;

public sealed record KennelImportRow(
    int RowNumber,
    string FacilityCode,
    string RoomCode,
    string KennelCode,
    string KennelName,
    int? GridRow,
    int? GridColumn,
    int StackLevel,
    int? DisplayOrder,
    bool IsActive,
    string? SourceReference,
    string? Notes);
