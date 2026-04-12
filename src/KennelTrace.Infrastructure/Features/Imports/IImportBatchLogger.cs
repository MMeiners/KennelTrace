namespace KennelTrace.Infrastructure.Features.Imports;

public interface IImportBatchLogger
{
    Task<ImportBatchLogResult> LogAsync(ImportBatchLogRequest request, CancellationToken cancellationToken = default);
}
