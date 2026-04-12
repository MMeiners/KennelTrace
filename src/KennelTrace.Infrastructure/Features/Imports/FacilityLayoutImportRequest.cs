using KennelTrace.Domain.Features.Imports;

namespace KennelTrace.Infrastructure.Features.Imports;

public sealed record FacilityLayoutImportRequest(
    string WorkbookPath,
    string? ExecutedByUserId = null,
    ImportBatchRunMode RunMode = ImportBatchRunMode.ValidateOnly);
