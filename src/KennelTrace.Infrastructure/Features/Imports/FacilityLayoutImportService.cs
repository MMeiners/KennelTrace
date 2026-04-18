using System.Security.Cryptography;
using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Facilities;
using KennelTrace.Domain.Features.Imports;
using KennelTrace.Domain.Features.Locations;
using KennelTrace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KennelTrace.Infrastructure.Features.Imports;

public sealed class FacilityLayoutImportService
{
    private readonly IWorkbookReader _workbookReader;
    private readonly FacilityLayoutImportValidator _validator;
    private readonly IImportBatchLogger _importBatchLogger;
    private readonly KennelTraceDbContext? _dbContext;

    public FacilityLayoutImportService(
        IWorkbookReader workbookReader,
        FacilityLayoutImportValidator validator,
        IImportBatchLogger importBatchLogger,
        KennelTraceDbContext? dbContext = null)
    {
        _workbookReader = workbookReader;
        _validator = validator;
        _importBatchLogger = importBatchLogger;
        _dbContext = dbContext;
    }

    public async Task<FacilityLayoutImportResult> ValidateAsync(
        FacilityLayoutImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkbookPath);

        var startedUtc = DateTime.UtcNow;
        var issues = new List<ImportValidationIssueRecord>();
        var workbook = await _workbookReader.ReadAsync(request.WorkbookPath, issues, cancellationToken);
        _validator.Validate(workbook, issues);

        var report = new ImportValidationReport(workbook, issues);
        var sourceFileName = Path.GetFileName(request.WorkbookPath);
        var sourceFileHash = ComputeSha256(request.WorkbookPath);

        if (request.RunMode == ImportBatchRunMode.Commit && report.IsValid)
        {
            return await CommitAsync(request, workbook, issues, startedUtc, sourceFileName, sourceFileHash, cancellationToken);
        }

        return await LogWithoutCommitAsync(request, report, issues, startedUtc, sourceFileName, sourceFileHash, cancellationToken);
    }

    public async Task<FacilityLayoutImportResult> ValidateAsync(
        FacilityLayoutImportUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.WorkbookStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceFileName);

        var startedUtc = DateTime.UtcNow;
        var issues = new List<ImportValidationIssueRecord>();
        var workbookBytes = await ReadAllBytesAsync(request.WorkbookStream, cancellationToken);

        await using var workbookStream = new MemoryStream(workbookBytes, writable: false);
        var workbook = await _workbookReader.ReadAsync(workbookStream, issues, cancellationToken);
        _validator.Validate(workbook, issues);

        var report = new ImportValidationReport(workbook, issues);
        var sourceFileName = Path.GetFileName(request.SourceFileName);
        var sourceFileHash = ComputeSha256(workbookBytes);
        var fileRequest = new FacilityLayoutImportRequest(
            WorkbookPath: sourceFileName,
            ExecutedByUserId: request.ExecutedByUserId,
            RunMode: request.RunMode);

        if (request.RunMode == ImportBatchRunMode.Commit && report.IsValid)
        {
            throw new InvalidOperationException("Stream-based uploads are limited to validate-only mode in the current admin UI slice.");
        }

        return await LogWithoutCommitAsync(fileRequest, report, issues, startedUtc, sourceFileName, sourceFileHash, cancellationToken);
    }

    private async Task<FacilityLayoutImportResult> CommitAsync(
        FacilityLayoutImportRequest request,
        ImportWorkbook workbook,
        List<ImportValidationIssueRecord> issues,
        DateTime startedUtc,
        string sourceFileName,
        string sourceFileHash,
        CancellationToken cancellationToken)
    {
        if (_dbContext is null)
        {
            throw new InvalidOperationException("Commit mode requires a configured KennelTraceDbContext.");
        }

        var facilityCodes = GetWorkbookFacilityCodes(workbook);
        if (facilityCodes.Count != 1)
        {
            issues.Add(new ImportValidationIssueRecord(
                ImportIssueSeverity.Error,
                "Facilities",
                "Commit mode requires a workbook scoped to exactly one facility."));

            var failedReport = new ImportValidationReport(workbook, issues);
            return await LogWithoutCommitAsync(request, failedReport, issues, startedUtc, sourceFileName, sourceFileHash, cancellationToken);
        }

        var targetFacilityCode = facilityCodes.Single();
        var facilityRow = workbook.Facilities.SingleOrDefault(x => x.FacilityCode.Equals(targetFacilityCode, StringComparison.OrdinalIgnoreCase));
        var existingFacility = await _dbContext.Facilities
            .SingleOrDefaultAsync(x => x.FacilityCode == new FacilityCode(targetFacilityCode), cancellationToken);

        if (facilityRow is null && existingFacility is null)
        {
            issues.Add(new ImportValidationIssueRecord(
                ImportIssueSeverity.Error,
                "Facilities",
                $"FacilityCode '{targetFacilityCode}' does not exist in the database and is not provided in the Facilities sheet."));

            var failedReport = new ImportValidationReport(workbook, issues);
            return await LogWithoutCommitAsync(request, failedReport, issues, startedUtc, sourceFileName, sourceFileHash, cancellationToken);
        }

        var report = new ImportValidationReport(workbook, issues);
        var pendingBatch = new ImportBatch(
            batchType: "FacilityLayout",
            sourceFileName: sourceFileName,
            runMode: request.RunMode,
            startedUtc: startedUtc,
            status: ImportBatchStatus.Pending,
            sourceFileHash: sourceFileHash,
            executedByUserId: request.ExecutedByUserId);

        _dbContext.ImportBatches.Add(pendingBatch);
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            var nowUtc = DateTime.UtcNow;

            var facility = await UpsertFacilityAsync(existingFacility, facilityRow, targetFacilityCode, nowUtc, cancellationToken);
            pendingBatch.AssociateFacility(facility.FacilityId);

            var locationsByCode = await UpsertLocationsAsync(facility, workbook, nowUtc, cancellationToken);
            await UpsertLocationLinksAsync(facility, workbook.LocationLinks, locationsByCode, nowUtc, cancellationToken);

            if (issues.Count > 0)
            {
                _dbContext.ImportIssues.AddRange(issues.Select(issue => new ImportIssue(
                    importBatchId: pendingBatch.ImportBatchId,
                    severity: issue.Severity,
                    sheetName: issue.SheetName,
                    message: issue.Message,
                    rowNumber: issue.RowNumber,
                    itemKey: issue.ItemKey)));
            }

            var displayText = BuildCommitDisplayText(sourceFileName, report);
            pendingBatch.Complete(DateTime.UtcNow, ImportBatchStatus.Succeeded, displayText);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new FacilityLayoutImportResult(sourceFileName, sourceFileHash, report, displayText, pendingBatch.ImportBatchId);
        }
        catch (Exception)
        {
            var batchId = pendingBatch.ImportBatchId;
            var facilityId = pendingBatch.FacilityId;

            if (_dbContext.Database.CurrentTransaction is not null)
            {
                await _dbContext.Database.RollbackTransactionAsync(cancellationToken);
            }

            _dbContext.ChangeTracker.Clear();

            var failedBatch = await _dbContext.ImportBatches.SingleAsync(x => x.ImportBatchId == batchId, cancellationToken);
            failedBatch.AssociateFacility(facilityId);
            failedBatch.Complete(
                DateTime.UtcNow,
                ImportBatchStatus.Failed,
                $"Commit failed for '{sourceFileName}'. See application logs for the exception details.");
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private async Task<FacilityLayoutImportResult> LogWithoutCommitAsync(
        FacilityLayoutImportRequest request,
        ImportValidationReport report,
        IReadOnlyList<ImportValidationIssueRecord> issues,
        DateTime startedUtc,
        string sourceFileName,
        string sourceFileHash,
        CancellationToken cancellationToken)
    {
        var displayText = ImportReportFormatter.Format(sourceFileName, report);
        var loggedBatch = await _importBatchLogger.LogAsync(
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

    private async Task<Facility> UpsertFacilityAsync(
        Facility? existingFacility,
        FacilityImportRow? facilityRow,
        string facilityCode,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var facility = existingFacility;

        if (facility is null)
        {
            if (facilityRow is null)
            {
                throw new InvalidOperationException($"Facility '{facilityCode}' could not be created because no Facilities sheet row was provided.");
            }

            facility = new Facility(
                new FacilityCode(facilityRow.FacilityCode),
                facilityRow.FacilityName,
                facilityRow.TimeZoneId,
                nowUtc,
                nowUtc,
                facilityRow.IsActive,
                facilityRow.Notes);
            _dbContext!.Facilities.Add(facility);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return facility;
        }

        if (facilityRow is not null)
        {
            facility.ApplyImport(facilityRow.FacilityName, facilityRow.TimeZoneId, facilityRow.IsActive, facilityRow.Notes, nowUtc);
            await _dbContext!.SaveChangesAsync(cancellationToken);
        }

        return facility;
    }

    private async Task<Dictionary<string, Location>> UpsertLocationsAsync(
        Facility facility,
        ImportWorkbook workbook,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var locationsByCode = await _dbContext!.Locations
            .Where(x => x.FacilityId == facility.FacilityId)
            .ToListAsync(cancellationToken);
        var locationsByCodeLookup = locationsByCode.ToDictionary(x => x.LocationCode.Value, StringComparer.OrdinalIgnoreCase);

        foreach (var room in workbook.Rooms)
        {
            if (!locationsByCodeLookup.TryGetValue(room.RoomCode, out var location))
            {
                location = new Location(
                    facility.FacilityId,
                    room.RoomType,
                    new LocationCode(room.RoomCode),
                    room.RoomName,
                    nowUtc,
                    nowUtc,
                    isActive: room.IsActive,
                    displayOrder: room.DisplayOrder,
                    notes: room.Notes);
                _dbContext.Locations.Add(location);
                locationsByCodeLookup.Add(room.RoomCode, location);
            }
            else
            {
                location.ApplyImport(
                    room.RoomType,
                    room.RoomName,
                    parentLocationId: null,
                    room.IsActive,
                    gridRow: null,
                    gridColumn: null,
                    stackLevel: 0,
                    room.DisplayOrder,
                    room.Notes,
                    nowUtc);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var room in workbook.Rooms)
        {
            var location = locationsByCodeLookup[room.RoomCode];
            int? parentLocationId = string.IsNullOrWhiteSpace(room.ParentLocationCode)
                ? null
                : locationsByCodeLookup[room.ParentLocationCode].LocationId;

            location.ApplyImport(
                room.RoomType,
                room.RoomName,
                parentLocationId,
                room.IsActive,
                gridRow: null,
                gridColumn: null,
                stackLevel: 0,
                room.DisplayOrder,
                room.Notes,
                nowUtc);
        }

        foreach (var kennel in workbook.Kennels)
        {
            var parentLocationId = locationsByCodeLookup[kennel.RoomCode].LocationId;

            if (!locationsByCodeLookup.TryGetValue(kennel.KennelCode, out var location))
            {
                location = new Location(
                    facility.FacilityId,
                    LocationType.Kennel,
                    new LocationCode(kennel.KennelCode),
                    kennel.KennelName,
                    nowUtc,
                    nowUtc,
                    parentLocationId,
                    kennel.IsActive,
                    kennel.GridRow,
                    kennel.GridColumn,
                    kennel.StackLevel,
                    kennel.DisplayOrder,
                    kennel.Notes);
                _dbContext.Locations.Add(location);
                locationsByCodeLookup.Add(kennel.KennelCode, location);
            }
            else
            {
                location.ApplyImport(
                    LocationType.Kennel,
                    kennel.KennelName,
                    parentLocationId,
                    kennel.IsActive,
                    kennel.GridRow,
                    kennel.GridColumn,
                    kennel.StackLevel,
                    kennel.DisplayOrder,
                    kennel.Notes,
                    nowUtc);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return locationsByCodeLookup;
    }

    private async Task UpsertLocationLinksAsync(
        Facility facility,
        IReadOnlyList<LocationLinkImportRow> importRows,
        IReadOnlyDictionary<string, Location> locationsByCode,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var existingLinks = await _dbContext!.LocationLinks
            .Where(x => x.FacilityId == facility.FacilityId)
            .ToListAsync(cancellationToken);
        var locationCodesById = locationsByCode.Values.ToDictionary(x => x.LocationId, x => x.LocationCode.Value);

        var existingByKey = existingLinks.ToDictionary(
            x => BuildLinkKey(locationCodesById[x.FromLocationId], locationCodesById[x.ToLocationId], x.LinkType),
            StringComparer.OrdinalIgnoreCase);
        var incomingLinks = ExpandImportLinks(importRows);

        foreach (var incoming in incomingLinks.Values)
        {
            var fromLocationId = locationsByCode[incoming.FromLocationCode].LocationId;
            var toLocationId = locationsByCode[incoming.ToLocationCode].LocationId;
            var key = BuildLinkKey(incoming.FromLocationCode, incoming.ToLocationCode, incoming.LinkType);

            if (existingByKey.TryGetValue(key, out var existing))
            {
                existing.ApplyImport(SourceType.Import, incoming.SourceReference, incoming.Notes, nowUtc);
                continue;
            }

            _dbContext.LocationLinks.Add(new LocationLink(
                facility.FacilityId,
                fromLocationId,
                toLocationId,
                incoming.LinkType,
                nowUtc,
                nowUtc,
                sourceType: SourceType.Import,
                sourceReference: incoming.SourceReference,
                notes: incoming.Notes));
        }

        foreach (var existing in existingLinks)
        {
            var key = BuildLinkKey(locationCodesById[existing.FromLocationId], locationCodesById[existing.ToLocationId], existing.LinkType);
            if (incomingLinks.ContainsKey(key))
            {
                continue;
            }

            existing.Deactivate(nowUtc);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyCollection<string> GetWorkbookFacilityCodes(ImportWorkbook workbook)
    {
        return workbook.Facilities.Select(x => x.FacilityCode)
            .Concat(workbook.Rooms.Select(x => x.FacilityCode))
            .Concat(workbook.Kennels.Select(x => x.FacilityCode))
            .Concat(workbook.LocationLinks.Select(x => x.FacilityCode))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static Dictionary<string, ExpandedLocationLink> ExpandImportLinks(IReadOnlyList<LocationLinkImportRow> importRows)
    {
        var expanded = new Dictionary<string, ExpandedLocationLink>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in importRows)
        {
            AddExpandedLink(expanded, row.FromLocationCode, row.ToLocationCode, row.LinkType, row.SourceReference, row.Notes);

            if (!row.CreateInverse)
            {
                continue;
            }

            AddExpandedLink(
                expanded,
                row.ToLocationCode,
                row.FromLocationCode,
                LinkTypeRules.InverseOf(row.LinkType),
                row.SourceReference,
                row.Notes);
        }

        return expanded;
    }

    private static void AddExpandedLink(
        IDictionary<string, ExpandedLocationLink> links,
        string fromLocationCode,
        string toLocationCode,
        LinkType linkType,
        string? sourceReference,
        string? notes)
    {
        var key = BuildLinkKey(fromLocationCode, toLocationCode, linkType);
        links[key] = new ExpandedLocationLink(fromLocationCode, toLocationCode, linkType, sourceReference, notes);
    }

    private static string BuildCommitDisplayText(string sourceFileName, ImportValidationReport report)
    {
        var validationText = ImportReportFormatter.Format(sourceFileName, report);
        return $"{validationText}{Environment.NewLine}Commit succeeded. Natural-key upserts were applied and the facility link set was reconciled.";
    }

    private static string BuildLinkKey(string fromKey, string toKey, LinkType linkType) =>
        $"{fromKey}->{toKey}/{linkType}";

    private static string ComputeSha256(string workbookPath)
    {
        using var stream = File.OpenRead(workbookPath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }

    private static string ComputeSha256(byte[] workbookBytes)
    {
        var hash = SHA256.HashData(workbookBytes);
        return Convert.ToHexString(hash);
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream workbookStream, CancellationToken cancellationToken)
    {
        if (workbookStream is MemoryStream memoryStream && memoryStream.TryGetBuffer(out var buffer))
        {
            return buffer.Array![..buffer.Count];
        }

        using var bufferedStream = new MemoryStream();
        await workbookStream.CopyToAsync(bufferedStream, cancellationToken);
        return bufferedStream.ToArray();
    }

    private sealed record ExpandedLocationLink(
        string FromLocationCode,
        string ToLocationCode,
        LinkType LinkType,
        string? SourceReference,
        string? Notes);
}
