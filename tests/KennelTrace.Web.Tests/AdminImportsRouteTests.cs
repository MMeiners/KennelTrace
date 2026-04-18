using Bunit;
using KennelTrace.Web.Components.Pages;
using KennelTrace.Web.Features.Imports.Admin;
using KennelTrace.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MudBlazor.Services;

namespace KennelTrace.Web.Tests;

public sealed class AdminImportsRouteTests : BunitContext
{
    private readonly FakeImportAdminHistoryReadService _historyReadService = new();
    private readonly FakeImportAdminService _adminService = new();
    private readonly TestAuthenticationStateProvider _authenticationStateProvider = new();

    public AdminImportsRouteTests()
    {
        Services.AddMudServices();
        Services.RemoveAll<IAuthorizationService>();
        Services.RemoveAll<IAuthorizationPolicyProvider>();
        Services.RemoveAll<IAuthorizationHandlerProvider>();
        Services.RemoveAll<IAuthorizationHandlerContextFactory>();
        Services.RemoveAll<IAuthorizationEvaluator>();
        Services.AddAuthorizationCore();
        Services.AddSingleton<AuthenticationStateProvider>(_authenticationStateProvider);
        Services.AddSingleton<IImportAdminHistoryReadService>(_historyReadService);
        Services.AddSingleton<IImportAdminService>(_adminService);
    }

    [Fact]
    public void Admin_Route_Renders_For_Admin_User()
    {
        _authenticationStateProvider.SetUser("admin-user", KennelTraceRoles.Admin, KennelTraceRoles.ReadOnly);

        var cut = Render(builder =>
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<AuthorizeRouteView>(0);
                childBuilder.AddAttribute(1, "RouteData", new RouteData(typeof(AdminImports), new Dictionary<string, object?>()));
                childBuilder.AddAttribute(2, "DefaultLayout", typeof(TestLayout));
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Contains("Admin Imports", cut.Markup);
        Assert.Equal(1, _historyReadService.ListRecentBatchesCallCount);
    }

    [Fact]
    public void Admin_Route_Is_Rejected_For_ReadOnly_User()
    {
        _authenticationStateProvider.SetUser("readonly-user", KennelTraceRoles.ReadOnly);

        var cut = Render(builder =>
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<AuthorizeRouteView>(0);
                childBuilder.AddAttribute(1, "RouteData", new RouteData(typeof(AdminImports), new Dictionary<string, object?>()));
                childBuilder.AddAttribute(2, "DefaultLayout", typeof(TestLayout));
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.DoesNotContain("Admin Imports", cut.Markup);
        Assert.Equal(0, _historyReadService.ListRecentBatchesCallCount);
    }

    private sealed class TestLayout : LayoutComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.AddContent(0, Body);
        }
    }

    private sealed class FakeImportAdminHistoryReadService : IImportAdminHistoryReadService
    {
        public int ListRecentBatchesCallCount { get; private set; }

        public Task<ImportBatchDetailView?> GetBatchAsync(long importBatchId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ImportBatchDetailView?>(null);

        public Task<IReadOnlyList<ImportBatchListItemView>> ListRecentBatchesAsync(int take = 15, CancellationToken cancellationToken = default)
        {
            ListRecentBatchesCallCount++;
            return Task.FromResult<IReadOnlyList<ImportBatchListItemView>>([]);
        }
    }

    private sealed class FakeImportAdminService : IImportAdminService
    {
        public Task<ImportAdminRunResult> ValidateAsync(ImportAdminValidateRequest request, System.Security.Claims.ClaimsPrincipal user, CancellationToken cancellationToken = default) =>
            Task.FromResult(ImportAdminRunResult.Forbidden());
    }
}
