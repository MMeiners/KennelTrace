using System.Security.Claims;
using System.Text;
using Bunit;
using KennelTrace.Domain.Features.Imports;
using KennelTrace.Web.Components.Pages;
using KennelTrace.Web.Features.Imports.Admin;
using KennelTrace.Web.Security;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace KennelTrace.Web.Tests;

public sealed class AdminImportsPageTests : BunitContext
{
    private readonly FakeImportAdminHistoryReadService _historyReadService = new();
    private readonly FakeImportAdminService _adminService = new();
    private readonly TestAuthenticationStateProvider _authenticationStateProvider = new();

    public AdminImportsPageTests()
    {
        Services.AddAuthorizationCore();
        Services.AddSingleton<AuthenticationStateProvider>(_authenticationStateProvider);
        Services.AddSingleton<IImportAdminHistoryReadService>(_historyReadService);
        Services.AddSingleton<IImportAdminService>(_adminService);

        _authenticationStateProvider.SetUser("admin-user", KennelTraceRoles.Admin, KennelTraceRoles.ReadOnly);
    }

    [Fact]
    public void Initial_Render_Shows_Honest_Empty_States()
    {
        var cut = Render<AdminImports>();

        Assert.Contains("Admin Imports", cut.Markup);
        Assert.Contains("No workbook run has been started from this page yet.", cut.Markup);
        Assert.Contains("No persisted import batches are available yet.", cut.Markup);
    }

    [Fact]
    public void Validate_Button_Requires_Selected_File()
    {
        var cut = Render<AdminImports>();

        cut.Find("[data-testid='validate-only-button']").Click();

        Assert.Contains("Select one canonical .xlsx workbook before running validate only.", cut.Markup);
        Assert.Equal(0, _adminService.RunCallCount);
    }

    [Fact]
    public void Commit_Button_Requires_Selected_File()
    {
        var cut = Render<AdminImports>();

        cut.Find("[data-testid='validate-and-commit-button']").Click();

        Assert.Contains("Select one canonical .xlsx workbook before running validate and commit.", cut.Markup);
        Assert.Equal(0, _adminService.RunCallCount);
    }

    [Fact]
    public void Wrong_Extension_Is_Rejected_Before_Service_Call()
    {
        var cut = Render<AdminImports>();

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("not-a-workbook", "layout.csv"));

        cut.WaitForAssertion(() =>
            Assert.Contains("Only canonical .xlsx workbooks are supported", cut.Markup));

        Assert.Equal(0, _adminService.RunCallCount);
    }

    [Fact]
    public void Successful_Validate_Run_Renders_Batch_Summary_And_Row_Level_Issues()
    {
        _adminService.NextResult = ImportAdminRunResult.Success(
            new ImportBatchDetailView(
                81,
                "FacilityLayout",
                "PHX_MAIN_Layout_20260418.xlsx",
                ImportBatchRunMode.ValidateOnly,
                ImportBatchStatus.Failed,
                new DateTime(2026, 4, 18, 17, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 18, 17, 0, 5, DateTimeKind.Utc),
                12,
                "PHX",
                "Phoenix Main",
                "Validation failed with 1 error and 1 warning.",
                1,
                1,
                [
                    new ImportIssueView(1001, ImportIssueSeverity.Error, "Rooms", 7, "ROOM-7", "RoomCode is required."),
                    new ImportIssueView(1002, ImportIssueSeverity.Warning, "Kennels", 9, "KEN-9", "Kennel has no grid placement.")
                ]),
            "Phoenix Main (PHX)");
        _historyReadService.ListRecentResult =
        [
            new ImportBatchListItemView(
                81,
                "PHX_MAIN_Layout_20260418.xlsx",
                ImportBatchRunMode.ValidateOnly,
                ImportBatchStatus.Failed,
                new DateTime(2026, 4, 18, 17, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 18, 17, 0, 5, DateTimeKind.Utc),
                12,
                "PHX",
                "Phoenix Main",
                "Validation failed with 1 error and 1 warning.",
                1,
                1)
        ];

        var cut = Render<AdminImports>();

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromBinary(
                CreateWorkbookBytes(),
                "PHX_MAIN_Layout_20260418.xlsx",
                lastChanged: null,
                contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
        cut.Find("[data-testid='validate-only-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("PHX_MAIN_Layout_20260418.xlsx", cut.Find("[data-testid='latest-import-summary']").TextContent);
            Assert.Contains("ValidateOnly", cut.Find("[data-testid='latest-import-summary']").TextContent);
            Assert.Contains("Phoenix Main (PHX)", cut.Find("[data-testid='latest-import-summary']").TextContent);
            Assert.Contains("RoomCode is required.", cut.Find("[data-testid='latest-import-issues-table']").TextContent);
            Assert.Contains("Kennel has no grid placement.", cut.Find("[data-testid='latest-import-issues-table']").TextContent);
        });

        Assert.Equal(1, _adminService.ValidateCallCount);
        Assert.Equal("PHX_MAIN_Layout_20260418.xlsx", _adminService.Requests.Single().SourceFileName);
        Assert.Equal("admin-user", _adminService.Requests.Single().ExecutedByUserId);
        Assert.Equal(ImportBatchRunMode.ValidateOnly, _adminService.Requests.Single().RunMode);
    }

    [Fact]
    public void Commit_Action_Wires_Commit_Run_Mode_And_Renders_Success_Result()
    {
        _adminService.NextResult = ImportAdminRunResult.Success(
            new ImportBatchDetailView(
                82,
                "FacilityLayout",
                "PHX_MAIN_Layout_20260418.xlsx",
                ImportBatchRunMode.Commit,
                ImportBatchStatus.Succeeded,
                new DateTime(2026, 4, 18, 18, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 18, 18, 0, 5, DateTimeKind.Utc),
                12,
                "PHX",
                "Phoenix Main",
                "Validation succeeded with 1 warning. Commit succeeded.",
                0,
                1,
                [
                    new ImportIssueView(1003, ImportIssueSeverity.Warning, "Kennels", 9, "KEN-9", "Kennel has no grid placement.")
                ]),
            "Phoenix Main (PHX)");
        _historyReadService.ListRecentResult =
        [
            new ImportBatchListItemView(
                82,
                "PHX_MAIN_Layout_20260418.xlsx",
                ImportBatchRunMode.Commit,
                ImportBatchStatus.Succeeded,
                new DateTime(2026, 4, 18, 18, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 18, 18, 0, 5, DateTimeKind.Utc),
                12,
                "PHX",
                "Phoenix Main",
                "Validation succeeded with 1 warning. Commit succeeded.",
                0,
                1)
        ];

        var cut = Render<AdminImports>();

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromBinary(
                CreateWorkbookBytes(),
                "PHX_MAIN_Layout_20260418.xlsx",
                lastChanged: null,
                contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
        cut.Find("[data-testid='validate-and-commit-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Commit", cut.Find("[data-testid='latest-import-summary']").TextContent);
            Assert.Contains("Succeeded", cut.Find("[data-testid='latest-import-summary']").TextContent);
            Assert.Contains("Kennel has no grid placement.", cut.Find("[data-testid='latest-import-issues-table']").TextContent);
            Assert.Contains("Commit", cut.Find("[data-testid='import-history-table']").TextContent);
        });

        Assert.Equal(1, _adminService.RunCallCount);
        Assert.Equal(ImportBatchRunMode.Commit, _adminService.Requests.Single().RunMode);
    }

    [Fact]
    public void Failed_Commit_Run_Renders_Batch_Summary_And_Error_Issues()
    {
        _adminService.NextResult = ImportAdminRunResult.Success(
            new ImportBatchDetailView(
                83,
                "FacilityLayout",
                "PHX_BAD_Layout_20260418.xlsx",
                ImportBatchRunMode.Commit,
                ImportBatchStatus.Failed,
                new DateTime(2026, 4, 18, 18, 30, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 18, 18, 30, 4, DateTimeKind.Utc),
                12,
                "PHX",
                "Phoenix Main",
                "Validation failed with 2 errors. Commit was not applied.",
                2,
                0,
                [
                    new ImportIssueView(1004, ImportIssueSeverity.Error, "Rooms", 4, "ROOM-X", "RoomCode is duplicated."),
                    new ImportIssueView(1005, ImportIssueSeverity.Error, "LocationLinks", 7, "KEN-1", "Conflicting inverse row was supplied.")
                ]),
            "Phoenix Main (PHX)");

        var cut = Render<AdminImports>();

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromBinary(
                CreateWorkbookBytes(),
                "PHX_BAD_Layout_20260418.xlsx",
                lastChanged: null,
                contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
        cut.Find("[data-testid='validate-and-commit-button']").Click();

        cut.WaitForAssertion(() =>
        {
            var summary = cut.Find("[data-testid='latest-import-summary']").TextContent;
            var issues = cut.Find("[data-testid='latest-import-issues-table']").TextContent;
            Assert.Contains("Commit", summary);
            Assert.Contains("Failed", summary);
            Assert.Contains("RoomCode is duplicated.", issues);
            Assert.Contains("Conflicting inverse row was supplied.", issues);
        });

        Assert.Equal(ImportBatchRunMode.Commit, _adminService.Requests.Single().RunMode);
    }

    [Fact]
    public void Recent_History_Renders_From_Persisted_Batch_Data()
    {
        _historyReadService.ListRecentResult =
        [
            new ImportBatchListItemView(
                41,
                "PHX_A.xlsx",
                ImportBatchRunMode.ValidateOnly,
                ImportBatchStatus.Succeeded,
                new DateTime(2026, 4, 18, 15, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 18, 15, 0, 1, DateTimeKind.Utc),
                11,
                "PHX",
                "Phoenix Main",
                "Validation succeeded.",
                0,
                1),
            new ImportBatchListItemView(
                40,
                "TUC_B.xlsx",
                ImportBatchRunMode.Commit,
                ImportBatchStatus.Succeeded,
                new DateTime(2026, 4, 18, 14, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 18, 14, 0, 2, DateTimeKind.Utc),
                null,
                null,
                null,
                "Validation succeeded. Commit succeeded.",
                0,
                2)
        ];

        var cut = Render<AdminImports>();

        var historyTable = cut.Find("[data-testid='import-history-table']");
        Assert.Contains("PHX_A.xlsx", historyTable.TextContent);
        Assert.Contains("Phoenix Main (PHX)", historyTable.TextContent);
        Assert.Contains("TUC_B.xlsx", historyTable.TextContent);
        Assert.Contains("Commit", historyTable.TextContent);
        Assert.Contains("Not resolved", historyTable.TextContent);
    }

    private static byte[] CreateWorkbookBytes() => Encoding.UTF8.GetBytes("placeholder-workbook");

    private sealed class FakeImportAdminHistoryReadService : IImportAdminHistoryReadService
    {
        public IReadOnlyList<ImportBatchListItemView> ListRecentResult { get; set; } = [];

        public Task<ImportBatchDetailView?> GetBatchAsync(long importBatchId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ImportBatchDetailView?>(null);

        public Task<IReadOnlyList<ImportBatchListItemView>> ListRecentBatchesAsync(int take = 15, CancellationToken cancellationToken = default) =>
            Task.FromResult(ListRecentResult);
    }

    private sealed class FakeImportAdminService : IImportAdminService
    {
        public List<ImportAdminRunRequest> Requests { get; } = [];

        public int RunCallCount => Requests.Count;
        public int ValidateCallCount => Requests.Count(x => x.RunMode == ImportBatchRunMode.ValidateOnly);

        public ImportAdminRunResult NextResult { get; set; } = ImportAdminRunResult.Forbidden();

        public async Task<ImportAdminRunResult> RunAsync(ImportAdminRunRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
        {
            using var captureStream = new MemoryStream();
            await request.WorkbookStream.CopyToAsync(captureStream, cancellationToken);

            Requests.Add(new ImportAdminRunRequest(
                new MemoryStream(captureStream.ToArray()),
                request.SourceFileName,
                request.ExecutedByUserId,
                request.RunMode));

            return NextResult;
        }
    }
}
