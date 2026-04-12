using KennelTrace.Domain.Common;

namespace KennelTrace.Domain.Features.Imports;

public sealed class ImportBatch
{
    private ImportBatch()
    {
    }

    public ImportBatch(
        string batchType,
        string sourceFileName,
        ImportBatchRunMode runMode,
        DateTime startedUtc,
        ImportBatchStatus status = ImportBatchStatus.Pending,
        int? facilityId = null,
        string? sourceFileHash = null,
        string? executedByUserId = null,
        string? summary = null,
        DateTime? completedUtc = null)
    {
        BatchType = Guard.RequiredText(batchType, nameof(batchType));
        SourceFileName = Guard.RequiredText(sourceFileName, nameof(sourceFileName));
        RunMode = runMode;
        StartedUtc = Guard.RequiredUtc(startedUtc, nameof(startedUtc));
        Status = status;
        FacilityId = facilityId;
        SourceFileHash = string.IsNullOrWhiteSpace(sourceFileHash) ? null : sourceFileHash.Trim();
        ExecutedByUserId = string.IsNullOrWhiteSpace(executedByUserId) ? null : executedByUserId.Trim();
        Summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();
        CompletedUtc = completedUtc is null ? null : Guard.RequiredUtc(completedUtc.Value, nameof(completedUtc));
    }

    public long ImportBatchId { get; private set; }

    public string BatchType { get; private set; } = null!;

    public int? FacilityId { get; private set; }

    public string SourceFileName { get; private set; } = null!;

    public string? SourceFileHash { get; private set; }

    public ImportBatchRunMode RunMode { get; private set; }

    public ImportBatchStatus Status { get; private set; }

    public DateTime StartedUtc { get; private set; }

    public DateTime? CompletedUtc { get; private set; }

    public string? ExecutedByUserId { get; private set; }

    public string? Summary { get; private set; }

    public void AssociateFacility(int? facilityId)
    {
        if (facilityId is not null)
        {
            Guard.Against(facilityId <= 0, "facilityId must be greater than zero when provided.");
        }

        FacilityId = facilityId;
    }

    public void Complete(DateTime completedUtc, ImportBatchStatus status, string? summary = null)
    {
        CompletedUtc = Guard.RequiredUtc(completedUtc, nameof(completedUtc));
        Guard.Against(CompletedUtc < StartedUtc, "CompletedUtc cannot be earlier than StartedUtc.");
        Status = status;
        Summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();
    }
}
