using System.Security.Claims;
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

public sealed class AdminAnimalMoveRouteTests : BunitContext
{
    private readonly TestAuthenticationStateProvider _authenticationStateProvider = new();
    private readonly FakeAnimalReadService _readService = new();
    private readonly FakeAnimalMovementAdminService _movementAdminService = new();

    public AdminAnimalMoveRouteTests()
    {
        Services.RemoveAll<IAuthorizationService>();
        Services.RemoveAll<IAuthorizationPolicyProvider>();
        Services.RemoveAll<IAuthorizationHandlerProvider>();
        Services.RemoveAll<IAuthorizationHandlerContextFactory>();
        Services.RemoveAll<IAuthorizationEvaluator>();
        Services.AddAuthorizationCore();
        Services.AddSingleton<AuthenticationStateProvider>(_authenticationStateProvider);
        Services.AddSingleton<IAnimalReadService>(_readService);
        Services.AddSingleton<IAnimalMovementAdminService>(_movementAdminService);
    }

    [Fact]
    public void Admin_Animal_Move_Route_Renders_For_Admin_User()
    {
        _authenticationStateProvider.SetUser("admin-user", KennelTraceRoles.Admin, KennelTraceRoles.ReadOnly);

        var cut = Render(builder =>
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<AuthorizeRouteView>(0);
                childBuilder.AddAttribute(1, "RouteData", new RouteData(typeof(AdminAnimalMove), new Dictionary<string, object?> { ["AnimalId"] = 42 }));
                childBuilder.AddAttribute(2, "DefaultLayout", typeof(TestLayout));
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Contains("Record Move", cut.Markup);
    }

    [Fact]
    public void Admin_Animal_Move_Route_Is_Rejected_For_ReadOnly_User()
    {
        _authenticationStateProvider.SetUser("readonly-user", KennelTraceRoles.ReadOnly);

        var cut = Render(builder =>
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<AuthorizeRouteView>(0);
                childBuilder.AddAttribute(1, "RouteData", new RouteData(typeof(AdminAnimalMove), new Dictionary<string, object?> { ["AnimalId"] = 42 }));
                childBuilder.AddAttribute(2, "DefaultLayout", typeof(TestLayout));
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.DoesNotContain("Record Move", cut.Markup);
        Assert.DoesNotContain("Save Move", cut.Markup);
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

        public Task<IReadOnlyList<AnimalMoveLocationOption>> ListMoveLocationsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AnimalMoveLocationOption>>([]);

        public Task<AnimalDetailResult?> GetAnimalDetailAsync(int animalId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AnimalDetailResult?>(null);
    }

    private sealed class FakeAnimalMovementAdminService : IAnimalMovementAdminService
    {
        public Task<RecordAnimalStayResult> RecordStayAsync(RecordAnimalStayRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default) =>
            Task.FromResult(RecordAnimalStayResult.Forbidden());
    }
}
