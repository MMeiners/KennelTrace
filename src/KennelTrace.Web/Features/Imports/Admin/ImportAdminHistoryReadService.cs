using KennelTrace.Domain.Features.Imports;
using KennelTrace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KennelTrace.Web.Features.Imports.Admin;

public interface IImportAdminHistoryReadService
{
    Task<ImportBatchDetailView?> GetBatchAsync(long importBatchId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ImportBatchListItemView>> ListRecentBatchesAsync(int take = 15, CancellationToken cancellationToken = default);
}

public sealed class ImportAdminHistoryReadService(KennelTraceDbContext dbContext) : IImportAdminHistoryReadService
{
    public async Task<ImportBatchDetailView?> GetBatchAsync(long importBatchId, CancellationToken cancellationToken = default)
    {
        var batch = await QueryBatches()
            .SingleOrDefaultAsync(x => x.ImportBatchId == importBatchId, cancellationToken);

        if (batch is null)
        {
            return null;
        }

        var issues = await dbContext.ImportIssues
            .AsNoTracking()
            .Where(x => x.ImportBatchId == importBatchId)
            .OrderBy(x => x.Severity)
            .ThenBy(x => x.SheetName)
            .ThenBy(x => x.RowNumber ?? int.MaxValue)
            .ThenBy(x => x.ImportIssueId)
            .Select(x => new ImportIssueView(
                x.ImportIssueId,
                x.Severity,
                x.SheetName,
                x.RowNumber,
                x.ItemKey,
                x.Message))
            .ToListAsync(cancellationToken);

        return new ImportBatchDetailView(
            batch.ImportBatchId,
            batch.BatchType,
            batch.SourceFileName,
            batch.RunMode,
            batch.Status,
            batch.StartedUtc,
            batch.CompletedUtc,
            batch.FacilityId,
            batch.FacilityCode,
            batch.FacilityName,
            batch.Summary,
            batch.ErrorCount,
            batch.WarningCount,
            issues);
    }

    public async Task<IReadOnlyList<ImportBatchListItemView>> ListRecentBatchesAsync(int take = 15, CancellationToken cancellationToken = default)
    {
        var normalizedTake = Math.Clamp(take, 1, 20);

        return await QueryBatches()
            .OrderByDescending(x => x.StartedUtc)
            .ThenByDescending(x => x.ImportBatchId)
            .Take(normalizedTake)
            .Select(x => new ImportBatchListItemView(
                x.ImportBatchId,
                x.SourceFileName,
                x.RunMode,
                x.Status,
                x.StartedUtc,
                x.CompletedUtc,
                x.FacilityId,
                x.FacilityCode,
                x.FacilityName,
                x.Summary,
                x.ErrorCount,
                x.WarningCount))
            .ToListAsync(cancellationToken);
    }

    private IQueryable<ImportBatchQueryRow> QueryBatches()
    {
        return from batch in dbContext.ImportBatches.AsNoTracking()
               join facility in dbContext.Facilities.AsNoTracking()
                   on batch.FacilityId equals facility.FacilityId into facilityJoin
               from facility in facilityJoin.DefaultIfEmpty()
               join issue in dbContext.ImportIssues.AsNoTracking()
                   on batch.ImportBatchId equals issue.ImportBatchId into issues
               select new ImportBatchQueryRow(
                   batch.ImportBatchId,
                   batch.BatchType,
                   batch.SourceFileName,
                   batch.RunMode,
                   batch.Status,
                   batch.StartedUtc,
                   batch.CompletedUtc,
                   batch.FacilityId,
                   facility != null ? facility.FacilityCode.Value : null,
                   facility != null ? facility.Name : null,
                   batch.Summary,
                   issues.Count(x => x.Severity == ImportIssueSeverity.Error),
                   issues.Count(x => x.Severity == ImportIssueSeverity.Warning));
    }

    private sealed record ImportBatchQueryRow(
        long ImportBatchId,
        string BatchType,
        string SourceFileName,
        ImportBatchRunMode RunMode,
        ImportBatchStatus Status,
        DateTime StartedUtc,
        DateTime? CompletedUtc,
        int? FacilityId,
        string? FacilityCode,
        string? FacilityName,
        string? Summary,
        int ErrorCount,
        int WarningCount);
}

public sealed record ImportBatchListItemView(
    long ImportBatchId,
    string SourceFileName,
    ImportBatchRunMode RunMode,
    ImportBatchStatus Status,
    DateTime StartedUtc,
    DateTime? CompletedUtc,
    int? FacilityId,
    string? FacilityCode,
    string? FacilityName,
    string? Summary,
    int ErrorCount,
    int WarningCount)
{
    public string? FacilityDisplay => string.IsNullOrWhiteSpace(FacilityCode)
        ? FacilityName
        : string.IsNullOrWhiteSpace(FacilityName)
            ? FacilityCode
            : $"{FacilityName} ({FacilityCode})";
}

public sealed record ImportBatchDetailView(
    long ImportBatchId,
    string BatchType,
    string SourceFileName,
    ImportBatchRunMode RunMode,
    ImportBatchStatus Status,
    DateTime StartedUtc,
    DateTime? CompletedUtc,
    int? FacilityId,
    string? FacilityCode,
    string? FacilityName,
    string? Summary,
    int ErrorCount,
    int WarningCount,
    IReadOnlyList<ImportIssueView> Issues)
{
    public string? FacilityDisplay => string.IsNullOrWhiteSpace(FacilityCode)
        ? FacilityName
        : string.IsNullOrWhiteSpace(FacilityName)
            ? FacilityCode
            : $"{FacilityName} ({FacilityCode})";
}

public sealed record ImportIssueView(
    long ImportIssueId,
    ImportIssueSeverity Severity,
    string SheetName,
    int? RowNumber,
    string? ItemKey,
    string Message);
