using KennelTrace.Domain.Features.Imports;

namespace KennelTrace.Infrastructure.Features.Imports;

public sealed record ImportBatchLogRequest(
    string BatchType,
    string SourceFileName,
    string SourceFileHash,
    ImportBatchRunMode RunMode,
    string? ExecutedByUserId,
    IReadOnlyList<ImportValidationIssueRecord> Issues,
    string Summary,
    DateTime StartedUtc,
    DateTime CompletedUtc);
