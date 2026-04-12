namespace KennelTrace.Domain.Features.Imports;

public sealed record FacilityImportRow(
    int RowNumber,
    string FacilityCode,
    string FacilityName,
    string TimeZoneId,
    bool IsActive,
    string? Notes);
