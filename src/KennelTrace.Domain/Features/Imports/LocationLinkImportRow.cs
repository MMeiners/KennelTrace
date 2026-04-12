using KennelTrace.Domain.Features.Locations;

namespace KennelTrace.Domain.Features.Imports;

public sealed record LocationLinkImportRow(
    int RowNumber,
    string FacilityCode,
    string FromLocationCode,
    string ToLocationCode,
    LinkType LinkType,
    bool CreateInverse,
    string? SourceReference,
    string? Notes);
