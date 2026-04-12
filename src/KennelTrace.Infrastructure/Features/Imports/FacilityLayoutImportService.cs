using System.Security.Cryptography;
using KennelTrace.Domain.Features.Imports;

namespace KennelTrace.Infrastructure.Features.Imports;

public sealed class FacilityLayoutImportService(
    IWorkbookReader workbookReader,
    FacilityLayoutImportValidator validator,
    IImportBatchLogger importBatchLogger)
{
    public async Task<FacilityLayoutImportResult> ValidateAsync(
        FacilityLayoutImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkbookPath);

        var startedUtc = DateTime.UtcNow;
        var issues = new List<ImportValidationIssueRecord>();
        var workbook = await workbookReader.ReadAsync(request.WorkbookPath, issues, cancellationToken);
        validator.Validate(workbook, issues);

        var report = new ImportValidationReport(workbook, issues);
        var sourceFileName = Path.GetFileName(request.WorkbookPath);
        var sourceFileHash = ComputeSha256(request.WorkbookPath);
        var displayText = ImportReportFormatter.Format(sourceFileName, report);
        var loggedBatch = await importBatchLogger.LogAsync(
            new ImportBatchLogRequest(
                BatchType: "FacilityLayout",
                SourceFileName: sourceFileName,
                SourceFileHash: sourceFileHash,
                RunMode: request.RunMode,
                ExecutedByUserId: request.ExecutedByUserId,
                Issues: issues,
                Summary: displayText,
                StartedUtc: startedUtc,
                CompletedUtc: DateTime.UtcNow),
            cancellationToken);

        return new FacilityLayoutImportResult(sourceFileName, sourceFileHash, report, displayText, loggedBatch.ImportBatchId);
    }

    private static string ComputeSha256(string workbookPath)
    {
        using var stream = File.OpenRead(workbookPath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }
}
