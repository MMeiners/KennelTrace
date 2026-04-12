namespace KennelTrace.Domain.Features.Imports;

public sealed record ImportValidationReport(
    ImportWorkbook Workbook,
    IReadOnlyList<ImportValidationIssueRecord> Issues)
{
    public int ErrorCount => Issues.Count(x => x.Severity == ImportIssueSeverity.Error);

    public int WarningCount => Issues.Count(x => x.Severity == ImportIssueSeverity.Warning);

    public bool IsValid => ErrorCount == 0;
}
