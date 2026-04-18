using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Animals;
using KennelTrace.Domain.Features.Facilities;
using KennelTrace.Domain.Features.Imports;
using KennelTrace.Domain.Features.Locations;
using KennelTrace.Infrastructure.Features.Imports;
using KennelTrace.Infrastructure.Persistence;
using KennelTrace.Web.Features.Imports.Admin;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace KennelTrace.Tests;

public sealed class SqlServerPersistenceIntegrationTests : IAsyncLifetime
{
    private readonly string _databaseName = $"KennelTrace_Test_{Guid.NewGuid():N}";
    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        var builder = new SqlConnectionStringBuilder(GetServerConnectionString())
        {
            InitialCatalog = _databaseName
        };

        _connectionString = builder.ConnectionString;

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();

        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task Can_Create_Database_From_Migrations_And_Persist_Valid_Rows()
    {
        await using var context = CreateContext();

        var migrations = await context.Database.GetAppliedMigrationsAsync();
        Assert.NotEmpty(migrations);

        var now = new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc);
        var facility = new Facility(new FacilityCode("FAC-1"), "Main Shelter", "America/Phoenix", now, now);
        context.Facilities.Add(facility);
        await context.SaveChangesAsync();

        var room = new Location(facility.FacilityId, LocationType.Room, new LocationCode("ROOM-1"), "Room 1", now, now);
        context.Locations.Add(room);
        await context.SaveChangesAsync();

        var kennel = new Location(facility.FacilityId, LocationType.Kennel, new LocationCode("KEN-1"), "Kennel 1", now, now, room.LocationId, gridRow: 0, gridColumn: 0);
        var animal = new Animal(new AnimalCode("A-100"), now, now, name: "Scout");
        context.AddRange(kennel, animal);
        await context.SaveChangesAsync();

        var movement = new MovementEvent(animal.AnimalId, kennel.LocationId, now, now, now, endUtc: now.AddHours(2));
        context.MovementEvents.Add(movement);
        await context.SaveChangesAsync();

        var stored = await context.MovementEvents.SingleAsync();
        Assert.Equal(animal.AnimalId, stored.AnimalId);
        Assert.Equal(kennel.LocationId, stored.LocationId);
    }

    [Fact]
    public async Task Active_Kennel_Grid_Positions_Must_Be_Unique_Within_A_Room()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc);

        var facility = new Facility(new FacilityCode("FAC-GRID"), "Grid Shelter", "America/Phoenix", now, now);
        context.Facilities.Add(facility);
        await context.SaveChangesAsync();

        var room = new Location(facility.FacilityId, LocationType.Room, new LocationCode("ROOM-GRID"), "Room Grid", now, now);
        context.Locations.Add(room);
        await context.SaveChangesAsync();

        context.Locations.Add(new Location(facility.FacilityId, LocationType.Kennel, new LocationCode("KEN-A"), "Kennel A", now, now, room.LocationId, gridRow: 1, gridColumn: 1));
        await context.SaveChangesAsync();

        context.Locations.Add(new Location(facility.FacilityId, LocationType.Kennel, new LocationCode("KEN-B"), "Kennel B", now, now, room.LocationId, gridRow: 1, gridColumn: 1));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Active_Directed_Location_Links_Must_Be_Unique_But_Inactive_History_Allows_Replacement()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc);

        var facility = new Facility(new FacilityCode("FAC-LINK"), "Link Shelter", "America/Phoenix", now, now);
        context.Facilities.Add(facility);
        await context.SaveChangesAsync();

        var leftRoom = new Location(facility.FacilityId, LocationType.Room, new LocationCode("ROOM-L"), "Left", now, now);
        var rightRoom = new Location(facility.FacilityId, LocationType.Room, new LocationCode("ROOM-R"), "Right", now, now);
        context.Locations.AddRange(leftRoom, rightRoom);
        await context.SaveChangesAsync();

        var activeLink = new LocationLink(facility.FacilityId, leftRoom.LocationId, rightRoom.LocationId, LinkType.Connected, now, now);
        context.LocationLinks.Add(activeLink);
        await context.SaveChangesAsync();

        context.LocationLinks.Add(new LocationLink(facility.FacilityId, leftRoom.LocationId, rightRoom.LocationId, LinkType.Connected, now, now));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();

        var existingLink = await context.LocationLinks.SingleAsync();
        existingLink.Deactivate(now.AddMinutes(5));
        await context.SaveChangesAsync();

        context.LocationLinks.Add(new LocationLink(facility.FacilityId, leftRoom.LocationId, rightRoom.LocationId, LinkType.Connected, now.AddMinutes(10), now.AddMinutes(10)));
        await context.SaveChangesAsync();

        Assert.Equal(2, await context.LocationLinks.CountAsync());
        Assert.Equal(1, await context.LocationLinks.CountAsync(x => x.IsActive));
    }

    [Fact]
    public async Task One_Open_Stay_Per_Animal_Is_Enforced_And_Half_Open_Queries_Do_Not_Treat_Handoff_As_Overlap()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc);

        var facility = new Facility(new FacilityCode("FAC-MOVE"), "Movement Shelter", "America/Phoenix", now, now);
        context.Facilities.Add(facility);
        await context.SaveChangesAsync();

        var room = new Location(facility.FacilityId, LocationType.Room, new LocationCode("ROOM-MOVE"), "Room Move", now, now);
        context.Locations.Add(room);
        await context.SaveChangesAsync();

        var firstKennel = new Location(facility.FacilityId, LocationType.Kennel, new LocationCode("KEN-1"), "Kennel 1", now, now, room.LocationId, gridRow: 0, gridColumn: 0);
        var secondKennel = new Location(facility.FacilityId, LocationType.Kennel, new LocationCode("KEN-2"), "Kennel 2", now, now, room.LocationId, gridRow: 0, gridColumn: 1);
        var animal = new Animal(new AnimalCode("A-MOVE"), now, now, name: "Mover");
        context.AddRange(firstKennel, secondKennel, animal);
        await context.SaveChangesAsync();

        context.MovementEvents.Add(new MovementEvent(animal.AnimalId, firstKennel.LocationId, now, now, now));
        await context.SaveChangesAsync();

        context.MovementEvents.Add(new MovementEvent(animal.AnimalId, secondKennel.LocationId, now.AddHours(1), now.AddHours(1), now.AddHours(1)));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        var openStay = await context.MovementEvents.SingleAsync();
        openStay.Close(now.AddHours(4), now.AddHours(4));
        await context.SaveChangesAsync();

        var secondStay = new MovementEvent(animal.AnimalId, secondKennel.LocationId, now.AddHours(4), now.AddHours(4), now.AddHours(4), endUtc: now.AddHours(8));
        context.MovementEvents.Add(secondStay);
        await context.SaveChangesAsync();

        var probeStart = now.AddHours(4);
        var probeEnd = now.AddHours(8);
        var overlappingIds = await context.MovementEvents
            .Where(x => x.StartUtc < probeEnd && probeStart < (x.EndUtc ?? DateTime.MaxValue))
            .Select(x => x.MovementEventId)
            .ToListAsync();

        Assert.DoesNotContain(openStay.MovementEventId, overlappingIds);
        Assert.Contains(secondStay.MovementEventId, overlappingIds);
    }

    [Fact]
    public async Task ValidateOnly_Warning_Workbook_Persists_Batch_And_Issues()
    {
        await using var context = CreateContext();
        var service = CreateImportService(context);

        var result = await service.ValidateAsync(new FacilityLayoutImportRequest(
            GetFixturePath("PHX_WARN_Layout_20260412_warnings.xlsx"),
            ExecutedByUserId: "import-tester"));

        Assert.True(result.IsValid);
        Assert.Equal(3, result.WarningCount);
        Assert.NotNull(result.ImportBatchId);

        var batch = await context.ImportBatches.SingleAsync(x => x.ImportBatchId == result.ImportBatchId);
        var issues = await context.ImportIssues
            .Where(x => x.ImportBatchId == result.ImportBatchId)
            .OrderBy(x => x.ImportIssueId)
            .ToListAsync();

        Assert.Equal(ImportBatchRunMode.ValidateOnly, batch.RunMode);
        Assert.Equal(ImportBatchStatus.Succeeded, batch.Status);
        Assert.Equal("import-tester", batch.ExecutedByUserId);
        Assert.Equal(3, issues.Count);
        Assert.All(issues, x => Assert.Equal(ImportIssueSeverity.Warning, x.Severity));
    }

    [Fact]
    public async Task Commit_Mode_Can_Persist_A_Clean_Workbook_And_Batch_Metadata()
    {
        await using var context = CreateContext();
        var service = CreateImportService(context);

        var result = await service.ValidateAsync(new FacilityLayoutImportRequest(
            GetFixturePath("PHX_MAIN_Layout_20260412.xlsx"),
            ExecutedByUserId: "import-admin",
            RunMode: ImportBatchRunMode.Commit));

        Assert.True(result.IsValid);
        Assert.Equal(0, result.ErrorCount);
        Assert.NotNull(result.ImportBatchId);
        Assert.Contains("Commit succeeded", result.DisplayText, StringComparison.Ordinal);

        var workbook = result.Report.Workbook;
        var facilityCode = workbook.Facilities.Single().FacilityCode;
        var facility = await context.Facilities.SingleAsync(x => x.FacilityCode == new FacilityCode(facilityCode));
        var locations = await context.Locations.Where(x => x.FacilityId == facility.FacilityId).ToListAsync();
        var activeLinks = await context.LocationLinks.Where(x => x.FacilityId == facility.FacilityId && x.IsActive).ToListAsync();
        var batch = await context.ImportBatches.SingleAsync(x => x.ImportBatchId == result.ImportBatchId);

        Assert.Equal(workbook.Rooms.Count + workbook.Kennels.Count, locations.Count);
        Assert.Equal(GetExpandedLinkCount(workbook.LocationLinks), activeLinks.Count);
        Assert.Equal(ImportBatchRunMode.Commit, batch.RunMode);
        Assert.Equal(ImportBatchStatus.Succeeded, batch.Status);
        Assert.Equal(facility.FacilityId, batch.FacilityId);
        Assert.Equal("import-admin", batch.ExecutedByUserId);
        Assert.Empty(await context.ImportIssues.Where(x => x.ImportBatchId == result.ImportBatchId).ToListAsync());
    }

    [Fact]
    public async Task Upload_Request_Commit_Mode_Can_Persist_A_Clean_Workbook_And_Batch_Metadata()
    {
        await using var context = CreateContext();
        var service = CreateImportService(context);
        await using var workbookStream = File.OpenRead(GetFixturePath("PHX_MAIN_Layout_20260412.xlsx"));

        var result = await service.ValidateAsync(new FacilityLayoutImportUploadRequest(
            workbookStream,
            "PHX_MAIN_Layout_20260412.xlsx",
            "import-admin",
            ImportBatchRunMode.Commit));

        Assert.True(result.IsValid);
        Assert.Equal(0, result.ErrorCount);
        Assert.NotNull(result.ImportBatchId);
        Assert.Contains("Commit succeeded", result.DisplayText, StringComparison.Ordinal);

        var workbook = result.Report.Workbook;
        var facilityCode = workbook.Facilities.Single().FacilityCode;
        var facility = await context.Facilities.SingleAsync(x => x.FacilityCode == new FacilityCode(facilityCode));
        var locations = await context.Locations.Where(x => x.FacilityId == facility.FacilityId).ToListAsync();
        var activeLinks = await context.LocationLinks.Where(x => x.FacilityId == facility.FacilityId && x.IsActive).ToListAsync();
        var batch = await context.ImportBatches.SingleAsync(x => x.ImportBatchId == result.ImportBatchId);

        Assert.Equal(workbook.Rooms.Count + workbook.Kennels.Count, locations.Count);
        Assert.Equal(GetExpandedLinkCount(workbook.LocationLinks), activeLinks.Count);
        Assert.Equal(ImportBatchRunMode.Commit, batch.RunMode);
        Assert.Equal(ImportBatchStatus.Succeeded, batch.Status);
        Assert.Equal(facility.FacilityId, batch.FacilityId);
        Assert.Equal("import-admin", batch.ExecutedByUserId);
        Assert.Empty(await context.ImportIssues.Where(x => x.ImportBatchId == result.ImportBatchId).ToListAsync());
    }

    [Fact]
    public async Task Commit_Mode_ReRun_Is_Idempotent_For_Locations_And_Links()
    {
        await using var context = CreateContext();
        var service = CreateImportService(context);
        var request = new FacilityLayoutImportRequest(
            GetFixturePath("PHX_MAIN_Layout_20260412.xlsx"),
            ExecutedByUserId: "import-admin",
            RunMode: ImportBatchRunMode.Commit);

        var firstResult = await service.ValidateAsync(request);
        var firstWorkbook = firstResult.Report.Workbook;
        var facilityCode = firstWorkbook.Facilities.Single().FacilityCode;
        var facility = await context.Facilities.SingleAsync(x => x.FacilityCode == new FacilityCode(facilityCode));

        var firstLocationCount = await context.Locations.CountAsync(x => x.FacilityId == facility.FacilityId);
        var firstTotalLinkCount = await context.LocationLinks.CountAsync(x => x.FacilityId == facility.FacilityId);
        var firstActiveLinkCount = await context.LocationLinks.CountAsync(x => x.FacilityId == facility.FacilityId && x.IsActive);

        var secondResult = await service.ValidateAsync(request);

        Assert.True(secondResult.IsValid);
        Assert.Equal(firstLocationCount, await context.Locations.CountAsync(x => x.FacilityId == facility.FacilityId));
        Assert.Equal(firstTotalLinkCount, await context.LocationLinks.CountAsync(x => x.FacilityId == facility.FacilityId));
        Assert.Equal(firstActiveLinkCount, await context.LocationLinks.CountAsync(x => x.FacilityId == facility.FacilityId && x.IsActive));
        Assert.Equal(1, await context.Facilities.CountAsync(x => x.FacilityCode == new FacilityCode(facilityCode)));
        Assert.Equal(2, await context.ImportBatches.CountAsync());
    }

    [Fact]
    public async Task Import_Admin_History_Reads_Batches_And_Facility_Display_From_Sql_Server()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 4, 18, 18, 0, 0, DateTimeKind.Utc);

        var facility = new Facility(new FacilityCode("PHX"), "Phoenix Main", "America/Phoenix", now, now);
        context.Facilities.Add(facility);
        await context.SaveChangesAsync();

        var batch = new ImportBatch(
            "FacilityLayout",
            "PHX_MAIN_Layout_20260418.xlsx",
            ImportBatchRunMode.ValidateOnly,
            now,
            status: ImportBatchStatus.Succeeded,
            facilityId: facility.FacilityId,
            executedByUserId: "import-admin",
            summary: "Validation completed.",
            completedUtc: now.AddSeconds(5));
        context.ImportBatches.Add(batch);
        await context.SaveChangesAsync();

        context.ImportIssues.AddRange(
            new ImportIssue(batch.ImportBatchId, ImportIssueSeverity.Warning, "Kennels", "Kennel has no grid placement.", 9, "KEN-9"),
            new ImportIssue(batch.ImportBatchId, ImportIssueSeverity.Error, "Rooms", "RoomCode is required.", 7, "ROOM-7"));
        await context.SaveChangesAsync();

        var service = new ImportAdminHistoryReadService(context);

        var recentBatches = await service.ListRecentBatchesAsync();
        var detail = await service.GetBatchAsync(batch.ImportBatchId);

        var listItem = Assert.Single(recentBatches);
        Assert.Equal(batch.ImportBatchId, listItem.ImportBatchId);
        Assert.Equal("PHX", listItem.FacilityCode);
        Assert.Equal("Phoenix Main", listItem.FacilityName);
        Assert.Equal("Phoenix Main (PHX)", listItem.FacilityDisplay);
        Assert.Equal(1, listItem.ErrorCount);
        Assert.Equal(1, listItem.WarningCount);

        Assert.NotNull(detail);
        Assert.Equal(batch.ImportBatchId, detail.ImportBatchId);
        Assert.Equal("PHX", detail.FacilityCode);
        Assert.Equal("Phoenix Main", detail.FacilityName);
        Assert.Equal(2, detail.Issues.Count);
        Assert.Contains(detail.Issues, x => x.Severity == ImportIssueSeverity.Error && x.SheetName == "Rooms");
        Assert.Contains(detail.Issues, x => x.Severity == ImportIssueSeverity.Warning && x.SheetName == "Kennels");
    }

    private KennelTraceDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<KennelTraceDbContext>()
            .UseSqlServer(_connectionString, sql => sql.MigrationsAssembly(typeof(KennelTraceDbContext).Assembly.FullName))
            .Options;

        return new KennelTraceDbContext(options);
    }

    private static string GetServerConnectionString()
    {
        var configured = Environment.GetEnvironmentVariable("KENNELTRACE_TEST_SQLSERVER");
        return string.IsNullOrWhiteSpace(configured)
            ? "Server=localhost;Integrated Security=true;Encrypt=False;TrustServerCertificate=true;MultipleActiveResultSets=True"
            : configured;
    }

    private static FacilityLayoutImportService CreateImportService(KennelTraceDbContext context)
    {
        return new FacilityLayoutImportService(
            new OpenXmlWorkbookReader(),
            new FacilityLayoutImportValidator(),
            new EfCoreImportBatchLogger(context),
            context);
    }

    private static string GetFixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "import_fixtures", fileName);

    private static int GetExpandedLinkCount(IReadOnlyList<LocationLinkImportRow> links)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var link in links)
        {
            seen.Add($"{link.FromLocationCode}->{link.ToLocationCode}/{link.LinkType}");

            if (!link.CreateInverse)
            {
                continue;
            }

            seen.Add($"{link.ToLocationCode}->{link.FromLocationCode}/{LinkTypeRules.InverseOf(link.LinkType)}");
        }

        return seen.Count;
    }
}
