using Bunit;
using KennelTrace.Infrastructure.Features.Animals.AnimalRecords;
using KennelTrace.Web.Components.Pages;
using KennelTrace.Web.Features.Animals.Admin;
using KennelTrace.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KennelTrace.Web.Tests;

public sealed class AdminAnimalsRouteTests : BunitContext
{
    private readonly TestAuthenticationStateProvider _authenticationStateProvider = new();
    private readonly FakeAnimalReadService _readService = new();
    private readonly FakeAnimalAdminService _adminService = new();

    public AdminAnimalsRouteTests()
    {
        Services.RemoveAll<IAuthorizationService>();
        Services.RemoveAll<IAuthorizationPolicyProvider>();
        Services.RemoveAll<IAuthorizationHandlerProvider>();
        Services.RemoveAll<IAuthorizationHandlerContextFactory>();
        Services.RemoveAll<IAuthorizationEvaluator>();
        Services.AddAuthorizationCore();
        Services.AddSingleton<AuthenticationStateProvider>(_authenticationStateProvider);
        Services.AddSingleton<IAnimalReadService>(_readService);
        Services.AddSingleton<IAnimalAdminService>(_adminService);
    }

    [Fact]
    public void Admin_Animals_Route_Renders_For_Admin_User()
    {
        _authenticationStateProvider.SetUser("admin-user", KennelTraceRoles.Admin, KennelTraceRoles.ReadOnly);

        var cut = Render(builder =>
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<AuthorizeRouteView>(0);
                childBuilder.AddAttribute(1, "RouteData", new RouteData(typeof(AdminAnimals), new Dictionary<string, object?>()));
                childBuilder.AddAttribute(2, "DefaultLayout", typeof(TestLayout));
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Contains("Animal Admin", cut.Markup);
    }

    [Fact]
    public void Admin_Animals_Route_Is_Rejected_For_ReadOnly_User()
    {
        _authenticationStateProvider.SetUser("readonly-user", KennelTraceRoles.ReadOnly);

        var cut = Render(builder =>
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<AuthorizeRouteView>(0);
                childBuilder.AddAttribute(1, "RouteData", new RouteData(typeof(AdminAnimals), new Dictionary<string, object?>()));
                childBuilder.AddAttribute(2, "DefaultLayout", typeof(TestLayout));
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.DoesNotContain("Animal Admin", cut.Markup);
        Assert.DoesNotContain("Save Animal", cut.Markup);
    }

    private sealed class TestLayout : LayoutComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.AddContent(0, Body);
        }
    }

    private sealed class FakeAnimalReadService : IAnimalReadService
    {
        public Task<IReadOnlyList<AnimalLookupRow>> LookupAnimalsAsync(string? searchText, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AnimalLookupRow>>([]);

        public Task<AnimalDetailResult?> GetAnimalDetailAsync(int animalId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AnimalDetailResult?>(null);
    }

    private sealed class FakeAnimalAdminService : IAnimalAdminService
    {
        public Task<AnimalSaveResult> SaveAsync(AnimalSaveRequest request, System.Security.Claims.ClaimsPrincipal user, CancellationToken cancellationToken = default) =>
            Task.FromResult(AnimalSaveResult.Forbidden());
    }
}
