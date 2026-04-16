using Bunit;
using KennelTrace.Web.Components.Pages;
using KennelTrace.Web.Features.Facilities.Admin;
using KennelTrace.Web.Features.Locations.Admin;
using KennelTrace.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MudBlazor.Services;

namespace KennelTrace.Web.Tests;

public sealed class AdminLayoutRouteTests : BunitContext
{
    private readonly FakeFacilityAdminService _service = new();
    private readonly FakeLocationAdminService _locationService = new();
    private readonly TestAuthenticationStateProvider _authenticationStateProvider = new();

    public AdminLayoutRouteTests()
    {
        Services.AddMudServices();
        Services.RemoveAll<IAuthorizationService>();
        Services.RemoveAll<IAuthorizationPolicyProvider>();
        Services.RemoveAll<IAuthorizationHandlerProvider>();
        Services.RemoveAll<IAuthorizationHandlerContextFactory>();
        Services.RemoveAll<IAuthorizationEvaluator>();
        Services.AddAuthorizationCore();
        Services.AddSingleton<AuthenticationStateProvider>(_authenticationStateProvider);
        Services.AddSingleton<IFacilityAdminService>(_service);
        Services.AddSingleton<ILocationAdminService>(_locationService);
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
                childBuilder.AddAttribute(1, "RouteData", new RouteData(typeof(AdminLayout), new Dictionary<string, object?>()));
                childBuilder.AddAttribute(2, "DefaultLayout", typeof(TestLayout));
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Contains("Layout Admin", cut.Markup);
        Assert.Equal(1, _service.ListFacilitiesCallCount);
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
                childBuilder.AddAttribute(1, "RouteData", new RouteData(typeof(AdminLayout), new Dictionary<string, object?>()));
                childBuilder.AddAttribute(2, "DefaultLayout", typeof(TestLayout));
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.DoesNotContain("Layout Admin", cut.Markup);
        Assert.DoesNotContain("Create Facility", cut.Markup);
        Assert.Equal(0, _service.ListFacilitiesCallCount);
        Assert.Equal(0, _locationService.GetFacilityCallCount);
    }

    private sealed class TestLayout : LayoutComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.AddContent(0, Body);
        }
    }

    private sealed class FakeFacilityAdminService : IFacilityAdminService
    {
        public int ListFacilitiesCallCount { get; private set; }

        public Task<IReadOnlyList<FacilityAdminListItem>> ListFacilitiesAsync(CancellationToken cancellationToken = default)
        {
            ListFacilitiesCallCount++;
            return Task.FromResult<IReadOnlyList<FacilityAdminListItem>>([]);
        }

        public Task<FacilitySaveResult> SaveAsync(FacilitySaveRequest request, System.Security.Claims.ClaimsPrincipal user, CancellationToken cancellationToken = default) =>
            Task.FromResult(FacilitySaveResult.Forbidden());
    }

    private sealed class FakeLocationAdminService : ILocationAdminService
    {
        public int GetFacilityCallCount { get; private set; }

        public Task<LocationAdminFacilityView?> GetFacilityAsync(int facilityId, CancellationToken cancellationToken = default)
        {
            GetFacilityCallCount++;
            return Task.FromResult<LocationAdminFacilityView?>(null);
        }

        public Task<LocationSaveResult> SaveAsync(LocationSaveRequest request, System.Security.Claims.ClaimsPrincipal user, CancellationToken cancellationToken = default) =>
            Task.FromResult(LocationSaveResult.Forbidden());
    }
}
