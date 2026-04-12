using KennelTrace.Domain.Features.Imports;

namespace KennelTrace.Infrastructure.Features.Imports;

public sealed record FacilityLayoutImportResult(
    string SourceFileName,
    string SourceFileHash,
    ImportValidationReport Report,
    string DisplayText,
    long? ImportBatchId)
{
    public bool IsValid => Report.IsValid;

    public int ErrorCount => Report.ErrorCount;

    public int WarningCount => Report.WarningCount;
}
