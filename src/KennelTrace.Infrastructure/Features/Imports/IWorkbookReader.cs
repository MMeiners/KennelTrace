using KennelTrace.Domain.Features.Imports;

namespace KennelTrace.Infrastructure.Features.Imports;

public interface IWorkbookReader
{
    Task<ImportWorkbook> ReadAsync(string workbookPath, ICollection<ImportValidationIssueRecord> issues, CancellationToken cancellationToken = default);
}
