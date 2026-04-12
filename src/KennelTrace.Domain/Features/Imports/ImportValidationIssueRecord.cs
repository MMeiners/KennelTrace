namespace KennelTrace.Domain.Features.Imports;

public sealed record ImportValidationIssueRecord(
    ImportIssueSeverity Severity,
    string SheetName,
    string Message,
    int? RowNumber = null,
    string? ItemKey = null);
