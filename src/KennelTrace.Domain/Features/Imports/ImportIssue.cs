using KennelTrace.Domain.Common;

namespace KennelTrace.Domain.Features.Imports;

public sealed class ImportIssue
{
    private ImportIssue()
    {
    }

    public ImportIssue(
        long importBatchId,
        ImportIssueSeverity severity,
        string sheetName,
        string message,
        int? rowNumber = null,
        string? itemKey = null)
    {
        Guard.Against(importBatchId <= 0, "importBatchId is required.");

        ImportBatchId = importBatchId;
        Severity = severity;
        SheetName = Guard.RequiredText(sheetName, nameof(sheetName));
        RowNumber = rowNumber;
        ItemKey = string.IsNullOrWhiteSpace(itemKey) ? null : itemKey.Trim();
        Message = Guard.RequiredText(message, nameof(message));
    }

    public long ImportIssueId { get; private set; }

    public long ImportBatchId { get; private set; }

    public ImportIssueSeverity Severity { get; private set; }

    public string SheetName { get; private set; } = null!;

    public int? RowNumber { get; private set; }

    public string? ItemKey { get; private set; }

    public string Message { get; private set; } = null!;
}
