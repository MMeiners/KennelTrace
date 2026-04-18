using KennelTrace.Domain.Common;
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
        var batch = await dbContext.ImportBatches
            .AsNoTracking()
            .Where(x => x.ImportBatchId == importBatchId)
            .Select(x => new ImportBatchQueryRow(
                x.ImportBatchId,
                x.BatchType,
                x.SourceFileName,
                x.RunMode,
                x.Status,
                x.StartedUtc,
                x.CompletedUtc,
                x.FacilityId,
                x.Summary,
                dbContext.ImportIssues.Count(issue => issue.ImportBatchId == x.ImportBatchId && issue.Severity == ImportIssueSeverity.Error),
                dbContext.ImportIssues.Count(issue => issue.ImportBatchId == x.ImportBatchId && issue.Severity == ImportIssueSeverity.Warning)))
            .SingleOrDefaultAsync(cancellationToken);

        if (batch is null)
        {
            return null;
        }

        var facilitiesById = await GetFacilitiesByIdAsync(
            batch.FacilityId is null ? [] : [batch.FacilityId.Value],
            cancellationToken);
        var facility = ResolveFacility(batch.FacilityId, facilitiesById);

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
            facility?.FacilityCode.Value,
            facility?.FacilityName,
            batch.Summary,
            batch.ErrorCount,
            batch.WarningCount,
            issues);
    }

    public async Task<IReadOnlyList<ImportBatchListItemView>> ListRecentBatchesAsync(int take = 15, CancellationToken cancellationToken = default)
    {
        var normalizedTake = Math.Clamp(take, 1, 20);
        var batches = await dbContext.ImportBatches
            .AsNoTracking()
            .OrderByDescending(x => x.StartedUtc)
            .ThenByDescending(x => x.ImportBatchId)
            .Take(normalizedTake)
            .Select(batch => new ImportBatchQueryRow(
                batch.ImportBatchId,
                batch.BatchType,
                batch.SourceFileName,
                batch.RunMode,
                batch.Status,
                batch.StartedUtc,
                batch.CompletedUtc,
                batch.FacilityId,
                batch.Summary,
                dbContext.ImportIssues.Count(x => x.ImportBatchId == batch.ImportBatchId && x.Severity == ImportIssueSeverity.Error),
                dbContext.ImportIssues.Count(x => x.ImportBatchId == batch.ImportBatchId && x.Severity == ImportIssueSeverity.Warning)))
            .ToListAsync(cancellationToken);

        var facilitiesById = await GetFacilitiesByIdAsync(
            batches.Where(x => x.FacilityId.HasValue).Select(x => x.FacilityId!.Value),
            cancellationToken);

        return batches
            .Select(batch =>
            {
                var facility = ResolveFacility(batch.FacilityId, facilitiesById);

                return new ImportBatchListItemView(
                    batch.ImportBatchId,
                    batch.SourceFileName,
                    batch.RunMode,
                    batch.Status,
                    batch.StartedUtc,
                    batch.CompletedUtc,
                    batch.FacilityId,
                    facility?.FacilityCode.Value,
                    facility?.FacilityName,
                    batch.Summary,
                    batch.ErrorCount,
                    batch.WarningCount);
            })
            .ToList();
    }

    private Task<Dictionary<int, FacilitySummaryRow>> GetFacilitiesByIdAsync(
        IEnumerable<int> facilityIds,
        CancellationToken cancellationToken)
    {
        var ids = facilityIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return Task.FromResult(new Dictionary<int, FacilitySummaryRow>());
        }

        return dbContext.Facilities
            .AsNoTracking()
            .Where(x => ids.Contains(x.FacilityId))
            .Select(x => new FacilitySummaryRow(
                x.FacilityId,
                x.FacilityCode,
                x.Name))
            .ToDictionaryAsync(x => x.FacilityId, cancellationToken);
    }

    private static FacilitySummaryRow? ResolveFacility(
        int? facilityId,
        IReadOnlyDictionary<int, FacilitySummaryRow> facilitiesById)
    {
        if (!facilityId.HasValue)
        {
            return null;
        }

        return facilitiesById.GetValueOrDefault(facilityId.Value);
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
        string? Summary,
        int ErrorCount,
        int WarningCount);

    private sealed record FacilitySummaryRow(
        int FacilityId,
        FacilityCode FacilityCode,
        string FacilityName);
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
