using System.Security.Claims;
using KennelTrace.Domain.Features.Imports;
using KennelTrace.Infrastructure.Features.Imports;
using KennelTrace.Web.Security;
using Microsoft.AspNetCore.Authorization;

namespace KennelTrace.Web.Features.Imports.Admin;

public interface IImportAdminService
{
    Task<ImportAdminRunResult> ValidateAsync(ImportAdminValidateRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default);
}

public sealed class ImportAdminService(
    FacilityLayoutImportService importService,
    IImportAdminHistoryReadService historyReadService,
    IAuthorizationService authorizationService) : IImportAdminService
{
    public async Task<ImportAdminRunResult> ValidateAsync(ImportAdminValidateRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.WorkbookStream);

        var authorizationResult = await authorizationService.AuthorizeAsync(user, resource: null, policyName: KennelTracePolicies.AdminOnly);
        if (!authorizationResult.Succeeded)
        {
            return ImportAdminRunResult.Forbidden();
        }

        var importResult = await importService.ValidateAsync(
            new FacilityLayoutImportUploadRequest(
                request.WorkbookStream,
                request.SourceFileName,
                request.ExecutedByUserId,
                ImportBatchRunMode.ValidateOnly),
            cancellationToken);

        if (importResult.ImportBatchId is null)
        {
            return ImportAdminRunResult.Failed("The validate-only run did not produce an import batch record.");
        }

        var batch = await historyReadService.GetBatchAsync(importResult.ImportBatchId.Value, cancellationToken);
        if (batch is null)
        {
            return ImportAdminRunResult.Failed("The validate-only run completed, but the persisted batch record could not be loaded.");
        }

        var resolvedFacilityDisplay = batch.FacilityDisplay ?? ResolveWorkbookFacilityCode(importResult.Report.Workbook);
        return ImportAdminRunResult.Success(batch, resolvedFacilityDisplay);
    }

    private static string? ResolveWorkbookFacilityCode(ImportWorkbook workbook)
    {
        var facilityCodes = workbook.Facilities.Select(x => x.FacilityCode)
            .Concat(workbook.Rooms.Select(x => x.FacilityCode))
            .Concat(workbook.Kennels.Select(x => x.FacilityCode))
            .Concat(workbook.LocationLinks.Select(x => x.FacilityCode))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return facilityCodes.Count == 1 ? facilityCodes[0] : null;
    }
}

public sealed record ImportAdminValidateRequest(
    Stream WorkbookStream,
    string SourceFileName,
    string? ExecutedByUserId);

public sealed record ImportAdminRunResult(
    ImportAdminRunStatus Status,
    ImportBatchDetailView? Batch,
    string? ResolvedFacilityDisplay,
    string? ErrorMessage)
{
    public static ImportAdminRunResult Success(ImportBatchDetailView batch, string? resolvedFacilityDisplay) =>
        new(ImportAdminRunStatus.Success, batch, resolvedFacilityDisplay, null);

    public static ImportAdminRunResult Forbidden() =>
        new(ImportAdminRunStatus.Forbidden, null, null, null);

    public static ImportAdminRunResult Failed(string errorMessage) =>
        new(ImportAdminRunStatus.Failed, null, null, errorMessage);
}

public enum ImportAdminRunStatus
{
    Success = 1,
    Forbidden = 2,
    Failed = 3
}
