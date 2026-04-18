using KennelTrace.Domain.Features.Imports;
using KennelTrace.Infrastructure.Features.Imports;

namespace KennelTrace.Tests;

public sealed class FacilityLayoutImportServiceTests
{
    private readonly OpenXmlWorkbookReader _workbookReader = new();
    private readonly FacilityLayoutImportValidator _validator = new();

    [Fact]
    public async Task Valid_Workbook_Succeeds_And_Logs_A_ValidateOnly_Batch()
    {
        var logger = new RecordingImportBatchLogger(101);
        var service = new FacilityLayoutImportService(_workbookReader, _validator, logger);

        var result = await service.ValidateAsync(new FacilityLayoutImportRequest(GetFixturePath("PHX_MAIN_Layout_20260412.xlsx"), "tester"));

        Assert.True(result.IsValid);
        Assert.Equal(0, result.ErrorCount);
        Assert.Equal(0, result.WarningCount);
        Assert.Equal(101, result.ImportBatchId);
        Assert.Single(result.Report.Workbook.Facilities);
        Assert.Equal(4, result.Report.Workbook.Rooms.Count);
        Assert.Equal(5, result.Report.Workbook.Kennels.Count);
        Assert.Equal(5, result.Report.Workbook.LocationLinks.Count);
        Assert.Contains("Validation succeeded", result.DisplayText);

        Assert.NotNull(logger.LastRequest);
        Assert.Equal(ImportBatchRunMode.ValidateOnly, logger.LastRequest!.RunMode);
        Assert.Equal("FacilityLayout", logger.LastRequest.BatchType);
        Assert.Empty(logger.LastRequest.Issues);
    }

    [Fact]
    public async Task Warning_Workbook_Succeeds_With_Readable_Warnings()
    {
        var logger = new RecordingImportBatchLogger(202);
        var service = new FacilityLayoutImportService(_workbookReader, _validator, logger);

        var result = await service.ValidateAsync(new FacilityLayoutImportRequest(GetFixturePath("PHX_WARN_Layout_20260412_warnings.xlsx")));

        Assert.True(result.IsValid);
        Assert.Equal(0, result.ErrorCount);
        Assert.Equal(3, result.WarningCount);
        Assert.Contains(result.Report.Issues, x => x.SheetName == "Kennels" && x.RowNumber == 2 && x.Message.Contains("no grid placement", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Report.Issues, x => x.SheetName == "Rooms" && x.RowNumber == 5 && x.Message.Contains("no active kennels", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Report.Issues, x => x.SheetName == "LocationLinks" && x.RowNumber == 3 && x.Message.Contains("uncertainty", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("- [Warning] Kennels row 2", result.DisplayText);
        Assert.Contains("- [Warning] Rooms row 5", result.DisplayText);
        Assert.Contains("- [Warning] LocationLinks row 3", result.DisplayText);
    }

    [Fact]
    public async Task Upload_Request_Succeeds_Without_Writing_To_Disk()
    {
        var logger = new RecordingImportBatchLogger(212);
        var service = new FacilityLayoutImportService(_workbookReader, _validator, logger);
        await using var workbookStream = File.OpenRead(GetFixturePath("PHX_MAIN_Layout_20260412.xlsx"));

        var result = await service.ValidateAsync(new FacilityLayoutImportUploadRequest(
            workbookStream,
            "PHX_MAIN_Layout_20260412.xlsx",
            "uploader"));

        Assert.True(result.IsValid);
        Assert.Equal("PHX_MAIN_Layout_20260412.xlsx", result.SourceFileName);
        Assert.Equal(212, result.ImportBatchId);
        Assert.NotNull(logger.LastRequest);
        Assert.Equal("PHX_MAIN_Layout_20260412.xlsx", logger.LastRequest!.SourceFileName);
        Assert.Equal("uploader", logger.LastRequest.ExecutedByUserId);
    }

    [Fact]
    public async Task Invalid_Row_Workbook_Fails_With_Explicit_Row_Level_Errors()
    {
        var service = new FacilityLayoutImportService(_workbookReader, _validator, new RecordingImportBatchLogger(303));

        var result = await service.ValidateAsync(new FacilityLayoutImportRequest(GetFixturePath("PHX_BAD_Layout_20260412_invalid_rows.xlsx")));

        Assert.False(result.IsValid);
        Assert.True(result.ErrorCount >= 11);
        Assert.Contains(result.Report.Issues, x => x.SheetName == "Rooms" && x.RowNumber == 5 && x.Message.Contains("duplicated", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Report.Issues, x => x.SheetName == "Rooms" && x.RowNumber == 6 && x.Message.Contains("must be one of", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Report.Issues, x => x.SheetName == "Rooms" && x.RowNumber == 7 && x.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Report.Issues, x => x.SheetName == "Rooms" && (x.RowNumber == 8 || x.RowNumber == 9) && x.Message.Contains("cycle", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Report.Issues, x => x.SheetName == "Kennels" && x.RowNumber == 4 && x.Message.Contains("RoomCode 'NO-ROOM' does not exist", StringComparison.Ordinal));
        Assert.Contains(result.Report.Issues, x => x.SheetName == "Kennels" && x.RowNumber == 5 && x.Message.Contains("GridRow '-1'", StringComparison.Ordinal));
        Assert.Contains(result.Report.Issues, x => x.SheetName == "Kennels" && x.RowNumber == 6 && x.Message.Contains("Grid position", StringComparison.Ordinal));
        Assert.Contains(result.Report.Issues, x => x.SheetName == "Kennels" && x.RowNumber == 7 && x.Message.Contains("LocationCode 'K-01' is duplicated", StringComparison.Ordinal));
        Assert.Contains(result.Report.Issues, x => x.SheetName == "LocationLinks" && x.RowNumber == 3 && x.Message.Contains("cannot both be 'K-01'", StringComparison.Ordinal));
        Assert.Contains(result.Report.Issues, x => x.SheetName == "LocationLinks" && x.RowNumber == 4 && x.Message.Contains("Adjacency link", StringComparison.Ordinal));
        Assert.Contains(result.Report.Issues, x => x.SheetName == "LocationLinks" && x.RowNumber == 5 && x.Message.Contains("must be one of", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Report.Issues, x => x.SheetName == "LocationLinks" && x.RowNumber == 6 && x.Message.Contains("duplicated", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Report.Issues, x => x.SheetName == "LocationLinks" && x.RowNumber == 7 && x.Message.Contains("inverse row must use", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Report.Issues, x => x.SheetName == "LocationLinks" && x.RowNumber == 9 && x.Message.Contains("Topology link", StringComparison.Ordinal));
        Assert.Contains(result.Report.Issues, x => x.SheetName == "LocationLinks" && x.RowNumber == 10 && x.Message.Contains("Cross-facility links are invalid", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Missing_Required_Sheet_Fails_Sheet_Validation()
    {
        var service = new FacilityLayoutImportService(_workbookReader, _validator, new RecordingImportBatchLogger(404));

        var result = await service.ValidateAsync(new FacilityLayoutImportRequest(GetFixturePath("PHX_MISS_Layout_20260412_missing_locationlinks.xlsx")));

        Assert.False(result.IsValid);
        Assert.Contains(result.Report.Issues, x => x.SheetName == "LocationLinks" && x.Message.Contains("Required sheet 'LocationLinks' is missing.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Bad_Header_Workbook_Fails_With_Cell_Level_Header_Messages()
    {
        var service = new FacilityLayoutImportService(_workbookReader, _validator, new RecordingImportBatchLogger(505));

        var result = await service.ValidateAsync(new FacilityLayoutImportRequest(GetFixturePath("PHX_HDR_Layout_20260412_bad_headers.xlsx")));

        Assert.False(result.IsValid);
        Assert.Contains(result.Report.Issues, x => x.Message.Contains("Rooms.A1", StringComparison.Ordinal) && x.Message.Contains("'Facility'", StringComparison.Ordinal));
        Assert.Contains(result.Report.Issues, x => x.Message.Contains("Rooms.D1", StringComparison.Ordinal) && x.Message.Contains("'RoomTyp'", StringComparison.Ordinal));
        Assert.Contains(result.Report.Issues, x => x.Message.Contains("Kennels.F1", StringComparison.Ordinal) && x.Message.Contains("'GridCol'", StringComparison.Ordinal));
        Assert.Contains(result.Report.Issues, x => x.Message.Contains("LocationLinks.D1", StringComparison.Ordinal) && x.Message.Contains("'LinkKind'", StringComparison.Ordinal));
    }

    private static string GetFixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "import_fixtures", fileName);

    private sealed class RecordingImportBatchLogger(long importBatchId) : IImportBatchLogger
    {
        public ImportBatchLogRequest? LastRequest { get; private set; }

        public Task<ImportBatchLogResult> LogAsync(ImportBatchLogRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            var status = request.Issues.Any(x => x.Severity == ImportIssueSeverity.Error)
                ? ImportBatchStatus.Failed
                : ImportBatchStatus.Succeeded;
            return Task.FromResult(new ImportBatchLogResult(importBatchId, status));
        }
    }
}
