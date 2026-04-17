using Bunit;
using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Locations;
using KennelTrace.Infrastructure.Features.Animals.AnimalRecords;
using KennelTrace.Infrastructure.Features.Tracing.TracePage;
using KennelTrace.Web.Components.Pages;
using KennelTrace.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MudBlazor;
using MudBlazor.Services;

namespace KennelTrace.Web.Tests;

public sealed class ContactTracePageTests : BunitContext
{
    private readonly FakeAnimalReadService _animalReadService = new();
    private readonly FakeTracePageReadService _tracePageReadService = new();
    private readonly TestAuthenticationStateProvider _authenticationStateProvider = new();

    public ContactTracePageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.RemoveAll<IKeyInterceptorService>();
        Services.AddSingleton<IKeyInterceptorService, TestKeyInterceptorService>();
        Services.RemoveAll<IAuthorizationService>();
        Services.RemoveAll<IAuthorizationPolicyProvider>();
        Services.RemoveAll<IAuthorizationHandlerProvider>();
        Services.RemoveAll<IAuthorizationHandlerContextFactory>();
        Services.RemoveAll<IAuthorizationEvaluator>();
        Services.AddAuthorizationCore();
        Services.AddSingleton<AuthenticationStateProvider>(_authenticationStateProvider);
        Services.AddSingleton<IAnimalReadService>(_animalReadService);
        Services.AddSingleton<ITracePageReadService>(_tracePageReadService);
    }

    [Fact]
    public void Contact_Trace_Route_Renders_For_ReadOnly_User()
    {
        SetReadOnlyUser();

        var cut = Render(builder =>
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<AuthorizeRouteView>(0);
                childBuilder.AddAttribute(1, "RouteData", new RouteData(typeof(ContactTrace), new Dictionary<string, object?>()));
                childBuilder.AddAttribute(2, "DefaultLayout", typeof(TestLayout));
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Contains("Contact Trace", cut.Markup);
    }

    [Fact]
    public void Contact_Trace_Route_Is_Rejected_For_Anonymous_User()
    {
        var cut = Render(builder =>
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<AuthorizeRouteView>(0);
                childBuilder.AddAttribute(1, "RouteData", new RouteData(typeof(ContactTrace), new Dictionary<string, object?>()));
                childBuilder.AddAttribute(2, "DefaultLayout", typeof(TestLayout));
                childBuilder.AddAttribute(3, "NotAuthorized", (RenderFragment<AuthenticationState>)(_ => notAuthorizedBuilder =>
                {
                    notAuthorizedBuilder.AddContent(0, "blocked");
                }));
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Contains("blocked", cut.Markup);
        Assert.DoesNotContain("Loading trace inputs from persisted data.", cut.Markup);
    }

    [Fact]
    public void Initial_Render_Shows_Loading_State_While_Inputs_Are_Loading()
    {
        SetReadOnlyUser();
        _tracePageReadService.HoldRequests = true;

        var cut = Render<ContactTrace>();

        Assert.Contains("Loading trace inputs from persisted data.", cut.Find("[data-testid='trace-loading-state']").TextContent);
    }

    [Fact]
    public void Loaded_Render_Shows_Disease_And_Scope_Selector_Options()
    {
        SetReadOnlyUser();
        _tracePageReadService.DiseaseProfiles =
        [
            new TraceDiseaseProfileOption(41, 7, new DiseaseCode("CIV"), "Canine Influenza", 72),
            new TraceDiseaseProfileOption(42, 8, new DiseaseCode("URI"), "Upper Respiratory", 24)
        ];
        _tracePageReadService.ScopeOptions =
        [
            new TraceLocationScopeOption(101, 12, new FacilityCode("PHX"), "Phoenix Shelter", new LocationCode("ROOM-A"), "Room A", LocationType.Room),
            new TraceLocationScopeOption(102, 12, new FacilityCode("PHX"), "Phoenix Shelter", new LocationCode("KEN-1"), "Kennel 1", LocationType.Kennel),
            new TraceLocationScopeOption(201, 34, new FacilityCode("TUC"), "Tucson Shelter", new LocationCode("ISO-1"), "Isolation 1", LocationType.Isolation)
        ];

        var cut = Render<ContactTrace>();

        Assert.Equal(1, _tracePageReadService.ListActiveDiseaseProfilesCallCount);
        Assert.Equal(1, _tracePageReadService.ListLocationScopeOptionsCallCount);
        Assert.NotNull(cut.Find("[data-testid='trace-disease-select']"));
        Assert.NotNull(cut.Find("[data-testid='trace-scope-facility-select']"));
        Assert.NotNull(cut.Find("[data-testid='trace-scope-location-select']"));
        Assert.Contains("Canine Influenza (CIV)", cut.Find("[data-testid='trace-disease-select']").InnerHtml);
        Assert.Contains("Upper Respiratory (URI)", cut.Find("[data-testid='trace-disease-select']").InnerHtml);
        Assert.Contains("Phoenix Shelter (PHX)", cut.Find("[data-testid='trace-scope-facility-select']").InnerHtml);
        Assert.Contains("Tucson Shelter (TUC)", cut.Find("[data-testid='trace-scope-facility-select']").InnerHtml);
        Assert.Contains("Room A (ROOM-A) - Room", cut.Find("[data-testid='trace-scope-location-select']").InnerHtml);
        Assert.Contains("Kennel 1 (KEN-1) - Kennel", cut.Find("[data-testid='trace-scope-location-select']").InnerHtml);
    }

    [Fact]
    public void Empty_States_Are_Shown_When_No_Profiles_Or_Scope_Options_Exist()
    {
        SetReadOnlyUser();
        _tracePageReadService.DiseaseProfiles = [];
        _tracePageReadService.ScopeOptions = [];

        var cut = Render<ContactTrace>();

        Assert.Contains("No active disease profiles are available yet.", cut.Find("[data-testid='trace-no-profiles']").TextContent);
        Assert.Contains("No persisted location scope options are available yet.", cut.Find("[data-testid='trace-no-scope-options']").TextContent);
    }

    [Fact]
    public void Facility_Selector_Only_Narrows_The_Scope_Location_List()
    {
        SetReadOnlyUser();
        _tracePageReadService.DiseaseProfiles =
        [
            new TraceDiseaseProfileOption(41, 7, new DiseaseCode("CIV"), "Canine Influenza", 72)
        ];
        _tracePageReadService.ScopeOptions =
        [
            new TraceLocationScopeOption(101, 12, new FacilityCode("PHX"), "Phoenix Shelter", new LocationCode("ROOM-A"), "Room A", LocationType.Room),
            new TraceLocationScopeOption(201, 34, new FacilityCode("TUC"), "Tucson Shelter", new LocationCode("ISO-1"), "Isolation 1", LocationType.Isolation)
        ];

        var cut = Render<ContactTrace>();

        cut.Find("[data-testid='trace-scope-facility-select']").Change("12");

        var scopeSelect = cut.Find("[data-testid='trace-scope-location-select']");
        Assert.Contains("Room A (ROOM-A) - Room", scopeSelect.InnerHtml);
        Assert.DoesNotContain("Isolation 1 (ISO-1) - Isolation", scopeSelect.InnerHtml);
        Assert.Contains("Canine Influenza (CIV)", cut.Find("[data-testid='trace-disease-select']").InnerHtml);
    }

    private void SetReadOnlyUser() =>
        _authenticationStateProvider.SetUser("readonly-user", KennelTraceRoles.ReadOnly);

    private sealed class FakeAnimalReadService : IAnimalReadService
    {
        public Task<IReadOnlyList<AnimalLookupRow>> LookupAnimalsAsync(string? searchText, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AnimalLookupRow>>(
            [
                new AnimalLookupRow(1, new AnimalCode("A-1"), "Milo", "Dog", true)
            ]);

        public Task<IReadOnlyList<AnimalMoveLocationOption>> ListMoveLocationsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AnimalMoveLocationOption>>([]);

        public Task<AnimalDetailResult?> GetAnimalDetailAsync(int animalId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AnimalDetailResult?>(null);
    }

    private sealed class FakeTracePageReadService : ITracePageReadService
    {
        public IReadOnlyList<TraceDiseaseProfileOption> DiseaseProfiles { get; set; } = [];

        public IReadOnlyList<TraceLocationScopeOption> ScopeOptions { get; set; } = [];

        public bool HoldRequests { get; set; }

        public int ListActiveDiseaseProfilesCallCount { get; private set; }

        public int ListLocationScopeOptionsCallCount { get; private set; }

        public Task<IReadOnlyList<TraceDiseaseProfileOption>> ListActiveDiseaseProfilesAsync(CancellationToken cancellationToken = default)
        {
            ListActiveDiseaseProfilesCallCount++;
            return HoldRequests
                ? new TaskCompletionSource<IReadOnlyList<TraceDiseaseProfileOption>>().Task
                : Task.FromResult(DiseaseProfiles);
        }

        public Task<IReadOnlyList<TraceLocationScopeOption>> ListLocationScopeOptionsAsync(CancellationToken cancellationToken = default)
        {
            ListLocationScopeOptionsCallCount++;
            return HoldRequests
                ? new TaskCompletionSource<IReadOnlyList<TraceLocationScopeOption>>().Task
                : Task.FromResult(ScopeOptions);
        }
    }

    private sealed class TestLayout : LayoutComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.AddContent(0, Body);
        }
    }

    private sealed class TestKeyInterceptorService : IKeyInterceptorService, IDisposable
    {
        public Task SubscribeAsync(IKeyInterceptorObserver observer, KeyInterceptorOptions options) => Task.CompletedTask;

        public Task SubscribeAsync(string elementId, KeyInterceptorOptions options, IKeyDownObserver? keyDown = null, IKeyUpObserver? keyUp = null) => Task.CompletedTask;

        public Task SubscribeAsync(string elementId, KeyInterceptorOptions options, Action<KeyboardEventArgs>? keyDown = null, Action<KeyboardEventArgs>? keyUp = null) => Task.CompletedTask;

        public Task SubscribeAsync(string elementId, KeyInterceptorOptions options, Func<KeyboardEventArgs, Task>? keyDown = null, Func<KeyboardEventArgs, Task>? keyUp = null) => Task.CompletedTask;

        public Task SubscribeAsync(string elementId, KeyInterceptorOptions options, Action<KeyMapBuilder> configure) => Task.CompletedTask;

        public Task UpdateKeyAsync(IKeyInterceptorObserver observer, KeyOptions option) => Task.CompletedTask;

        public Task UpdateKeyAsync(string elementId, KeyOptions option) => Task.CompletedTask;

        public Task UnsubscribeAsync(IKeyInterceptorObserver observer) => Task.CompletedTask;

        public Task UnsubscribeAsync(string elementId) => Task.CompletedTask;

        public Task DispatchAsync(string elementId, KeyEventKind keyEventKind, KeyboardEventArgs args) => Task.CompletedTask;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
