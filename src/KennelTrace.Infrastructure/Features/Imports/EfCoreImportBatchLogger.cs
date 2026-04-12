using KennelTrace.Domain.Features.Imports;
using KennelTrace.Infrastructure.Persistence;

namespace KennelTrace.Infrastructure.Features.Imports;

public sealed class EfCoreImportBatchLogger(KennelTraceDbContext dbContext) : IImportBatchLogger
{
    public async Task<ImportBatchLogResult> LogAsync(ImportBatchLogRequest request, CancellationToken cancellationToken = default)
    {
        var status = request.Issues.Any(x => x.Severity == ImportIssueSeverity.Error)
            ? ImportBatchStatus.Failed
            : ImportBatchStatus.Succeeded;

        var batch = new ImportBatch(
            batchType: request.BatchType,
            sourceFileName: request.SourceFileName,
            runMode: request.RunMode,
            startedUtc: request.StartedUtc,
            status: ImportBatchStatus.Pending,
            sourceFileHash: request.SourceFileHash,
            executedByUserId: request.ExecutedByUserId);

        dbContext.ImportBatches.Add(batch);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (request.Issues.Count > 0)
        {
            dbContext.ImportIssues.AddRange(request.Issues.Select(issue => new ImportIssue(
                importBatchId: batch.ImportBatchId,
                severity: issue.Severity,
                sheetName: issue.SheetName,
                message: issue.Message,
                rowNumber: issue.RowNumber,
                itemKey: issue.ItemKey)));
        }

        batch.Complete(request.CompletedUtc, status, request.Summary);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ImportBatchLogResult(batch.ImportBatchId, status);
    }
}
