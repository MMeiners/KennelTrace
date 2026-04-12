using KennelTrace.Domain.Common;

namespace KennelTrace.Domain.Features.Imports;

public sealed class ImportIssue
{
    public ImportIssue(
        Guid id,
        Guid importBatchId,
        string fileName,
        string sheetName,
        int rowNumber,
        string message,
        bool isError)
    {
        Id = Guard.RequiredId(id, nameof(id));
        ImportBatchId = Guard.RequiredId(importBatchId, nameof(importBatchId));
        FileName = Guard.RequiredText(fileName, nameof(fileName));
        SheetName = Guard.RequiredText(sheetName, nameof(sheetName));
        RowNumber = Guard.Positive(rowNumber, nameof(rowNumber));
        Message = Guard.RequiredText(message, nameof(message));
        IsError = isError;
    }

    public Guid Id { get; }

    public Guid ImportBatchId { get; }

    public string FileName { get; }

    public string SheetName { get; }

    public int RowNumber { get; }

    public string Message { get; }

    public bool IsError { get; }
}
