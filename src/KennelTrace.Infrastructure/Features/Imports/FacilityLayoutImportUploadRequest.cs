using KennelTrace.Domain.Features.Imports;

namespace KennelTrace.Infrastructure.Features.Imports;

public sealed record FacilityLayoutImportUploadRequest(
    Stream WorkbookStream,
    string SourceFileName,
    string? ExecutedByUserId = null,
    ImportBatchRunMode RunMode = ImportBatchRunMode.ValidateOnly);
