using System.Globalization;
using Bunit;
using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Locations;
using KennelTrace.Domain.Features.Tracing;
using KennelTrace.Infrastructure.Features.Animals.AnimalRecords;
using KennelTrace.Infrastructure.Features.Tracing.ContactTracing;
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
    private readonly FakeContactTraceService _contactTraceService = new();
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
        Services.AddSingleton<IContactTraceService>(_contactTraceService);
        Services.AddSingleton<ITracePageReadService>(_tracePageReadService);

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
        _animalReadService.LookupResults =
        [
            new AnimalLookupRow(1, new AnimalCode("A-1"), "Milo", "Dog", true),
            new AnimalLookupRow(2, new AnimalCode("A-2"), null, "Dog", true)
        ];
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
    public void Loaded_Render_Shows_Input_Selectors_And_Idle_State()
    {
        SetReadOnlyUser();

        var cut = Render<ContactTrace>();

        Assert.Equal(1, _tracePageReadService.ListActiveDiseaseProfilesCallCount);
        Assert.Equal(1, _tracePageReadService.ListLocationScopeOptionsCallCount);
        Assert.NotNull(cut.Find("[data-testid='trace-input-form']"));
        Assert.Contains("Canine Influenza (CIV)", cut.Find("[data-testid='trace-disease-select']").InnerHtml);
        Assert.Contains("Phoenix Shelter (PHX)", cut.Find("[data-testid='trace-scope-facility-select']").InnerHtml);
        Assert.Contains("Room A (ROOM-A) - Room", cut.Find("[data-testid='trace-scope-location-select']").InnerHtml);
        Assert.Contains("Select a profile, source animal, window, and optional scope, then run the trace.", cut.Find("[data-testid='trace-idle-state']").TextContent);
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

        var cut = Render<ContactTrace>();

        cut.Find("[data-testid='trace-scope-facility-select']").Change("12");

        var scopeSelect = cut.Find("[data-testid='trace-scope-location-select']");
        Assert.Contains("Room A (ROOM-A) - Room", scopeSelect.InnerHtml);
        Assert.DoesNotContain("Isolation 1 (ISO-1) - Isolation", scopeSelect.InnerHtml);
        Assert.Contains("This only narrows the scope-location picker. It is not sent as a second trace filter.", cut.Markup);
    }

    [Fact]
    public void Submit_Shows_Validation_Messages_For_Missing_Required_Fields()
    {
        SetReadOnlyUser();

        var cut = Render<ContactTrace>();

        cut.Find("[data-testid='trace-run-button']").Click();

        Assert.Contains("Select a disease profile before running a trace.", cut.Markup);
        Assert.Contains("Select a persisted source animal before running a trace.", cut.Markup);
        Assert.Contains("Trace window start must be a valid UTC date and time.", cut.Markup);
        Assert.Contains("Trace window end must be a valid UTC date and time.", cut.Markup);
        Assert.Empty(_contactTraceService.Requests);
    }

    [Fact]
    public void Submit_Rejects_End_Less_Than_Or_Equal_To_Start()
    {
        SetReadOnlyUser();

        var cut = Render<ContactTrace>();
        SelectProfileAndAnimal(cut);
        cut.Find("[data-testid='trace-window-start']").Input("2026-04-17T12:00");
        cut.Find("[data-testid='trace-window-end']").Input("2026-04-17T12:00");

        cut.Find("[data-testid='trace-run-button']").Click();

        Assert.Contains("Trace window end must be later than the trace window start.", cut.Markup);
        Assert.Empty(_contactTraceService.Requests);
    }

    [Fact]
    public void Selecting_A_Profile_Prefills_Window_When_User_Has_Not_Edited_It()
    {
        SetReadOnlyUser();

        var cut = Render<ContactTrace>();

        cut.Find("[data-testid='trace-disease-select']").Change("41");

        var startValue = cut.Find("[data-testid='trace-window-start']").GetAttribute("value");
        var endValue = cut.Find("[data-testid='trace-window-end']").GetAttribute("value");
        Assert.False(string.IsNullOrWhiteSpace(startValue));
        Assert.False(string.IsNullOrWhiteSpace(endValue));

        var startUtc = ParseDateTimeLocal(startValue);
        var endUtc = ParseDateTimeLocal(endValue);
        Assert.Equal(TimeSpan.FromHours(72), endUtc - startUtc);
    }

    [Fact]
    public void Selecting_A_New_Profile_Does_Not_Overwrite_A_Manually_Edited_Window()
    {
        SetReadOnlyUser();

        var cut = Render<ContactTrace>();

        cut.Find("[data-testid='trace-disease-select']").Change("41");
        cut.Find("[data-testid='trace-window-start']").Input("2026-04-10T08:15");
        cut.Find("[data-testid='trace-window-end']").Input("2026-04-11T09:45");

        cut.Find("[data-testid='trace-disease-select']").Change("42");

        Assert.Equal("2026-04-10T08:15", cut.Find("[data-testid='trace-window-start']").GetAttribute("value"));
        Assert.Equal("2026-04-11T09:45", cut.Find("[data-testid='trace-window-end']").GetAttribute("value"));
    }

    [Fact]
    public void Run_Trace_Invokes_Service_Shows_Loading_And_Renders_Success_Summary()
    {
        SetReadOnlyUser();
        _contactTraceService.PendingResult = new TaskCompletionSource<ContactTraceResult>();

        var cut = Render<ContactTrace>();
        SelectProfileAndAnimal(cut);
        cut.Find("[data-testid='trace-window-start']").Input("2026-04-16T00:00");
        cut.Find("[data-testid='trace-window-end']").Input("2026-04-17T00:00");
        cut.Find("[data-testid='trace-scope-facility-select']").Change("12");
        cut.Find("[data-testid='trace-scope-location-select']").Change("101");

        cut.Find("[data-testid='trace-run-button']").Click();

        Assert.Single(_contactTraceService.Requests);
        Assert.Equal(41, _contactTraceService.Requests[0].DiseaseTraceProfileId);
        Assert.Equal(1, _contactTraceService.Requests[0].SourceAnimalId);
        Assert.Null(_contactTraceService.Requests[0].FacilityId);
        Assert.Equal(101, _contactTraceService.Requests[0].LocationScopeLocationId);
        Assert.Contains("Running contact trace against persisted movement and location data.", cut.Find("[data-testid='trace-running-state']").TextContent);

        _contactTraceService.PendingResult.SetResult(CreateSuccessResult());

        cut.WaitForAssertion(() =>
        {
            var summary = cut.Find("[data-testid='trace-success-summary']").TextContent;
            Assert.Contains("Canine Influenza (CIV)", summary);
            Assert.Contains("A-1 - Milo", summary);
            Assert.Contains("2026-04-16 00:00 UTC to 2026-04-17 00:00 UTC", summary);
            Assert.Contains("Room A (ROOM-A) - Room", summary);
            Assert.Contains("1 source stay(s), 2 impacted location(s), 1 impacted animal(s).", summary);
            Assert.NotNull(cut.Find("[data-testid='trace-locations-table']"));
            Assert.NotNull(cut.Find("[data-testid='trace-result-tabs']"));
            Assert.DoesNotContain("Why Included", cut.Markup);
        });
    }

    [Fact]
    public void Successful_Run_With_No_Impacted_Results_Shows_Empty_State()
    {
        SetReadOnlyUser();
        _contactTraceService.Result = new ContactTraceResult(41, [9001], [], []);

        var cut = Render<ContactTrace>();
        SelectProfileAndAnimal(cut);
        cut.Find("[data-testid='trace-window-start']").Input("2026-04-16T00:00");
        cut.Find("[data-testid='trace-window-end']").Input("2026-04-17T00:00");

        cut.Find("[data-testid='trace-run-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("No impacted locations or animals were found for the selected trace input.", cut.Find("[data-testid='trace-empty-state']").TextContent);
            Assert.Contains("No impacted locations were returned for this trace.", cut.Find("[data-testid='trace-locations-empty-state']").TextContent);
            Assert.Contains("1 source stay(s), 0 impacted location(s), 0 impacted animal(s).", cut.Find("[data-testid='trace-success-summary']").TextContent);
        });

        ActivateTab(cut, "Impacted Animals");
        Assert.Contains("No impacted animals were returned for this trace.", cut.Find("[data-testid='trace-animals-empty-state']").TextContent);
    }

    [Fact]
    public void Service_Error_Is_Rendered_When_Trace_Run_Fails()
    {
        SetReadOnlyUser();
        _contactTraceService.ExceptionToThrow = new DomainValidationException("No source stays were found for the selected animal in the requested trace window.");

        var cut = Render<ContactTrace>();
        SelectProfileAndAnimal(cut);
        cut.Find("[data-testid='trace-window-start']").Input("2026-04-16T00:00");
        cut.Find("[data-testid='trace-window-end']").Input("2026-04-17T00:00");

        cut.Find("[data-testid='trace-run-button']").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains(
                "No source stays were found for the selected animal in the requested trace window.",
                cut.Find("[data-testid='trace-service-error-state']").TextContent));
    }

    [Fact]
    public void Successful_Run_Renders_Impacted_Location_Row_Content_And_Reason_Chips()
    {
        SetReadOnlyUser();

        var cut = Render<ContactTrace>();
        SelectProfileAndAnimal(cut);
        cut.Find("[data-testid='trace-window-start']").Input("2026-04-16T00:00");
        cut.Find("[data-testid='trace-window-end']").Input("2026-04-17T00:00");
        cut.Find("[data-testid='trace-scope-location-select']").Change("101");

        cut.Find("[data-testid='trace-run-button']").Click();

        cut.WaitForAssertion(() =>
        {
            var locationTable = cut.Find("[data-testid='trace-locations-table']").TextContent;
            Assert.Contains("Room A (ROOM-A)", locationTable);
            Assert.Contains("Room in Phoenix Shelter (PHX)", locationTable);
            Assert.Contains("Exact location", locationTable);
            Assert.Contains("Direct match", locationTable);
            Assert.Contains("Same location", cut.Find("[data-testid='trace-location-reason-101-SameLocation']").TextContent);

            Assert.Contains("Kennel 1 (KEN-1)", locationTable);
            Assert.Contains("Scoped descendant", locationTable);
            Assert.Contains("Scoped by Room A (ROOM-A)", cut.Find("[data-testid='trace-location-scope-102']").TextContent);
            Assert.Contains("Depth 1", locationTable);
            Assert.Contains("Adjacent right", locationTable);
            Assert.Contains("Adjacent", cut.Find("[data-testid='trace-location-reason-102-Adjacent']").TextContent);
        });
    }

    [Fact]
    public void Successful_Run_Renders_Impacted_Animal_Row_Content_With_Stable_Detail_Link()
    {
        SetReadOnlyUser();

        var cut = Render<ContactTrace>();
        SelectProfileAndAnimal(cut);
        cut.Find("[data-testid='trace-window-start']").Input("2026-04-16T00:00");
        cut.Find("[data-testid='trace-window-end']").Input("2026-04-17T00:00");

        cut.Find("[data-testid='trace-run-button']").Click();

        cut.WaitForAssertion(() =>
        {
            ActivateTab(cut, "Impacted Animals");

            var animalLink = cut.Find("[data-testid='trace-animal-link-77']");
            Assert.Equal("/animals/77", animalLink.GetAttribute("href"));
            Assert.Contains("A-77 - Luna", animalLink.TextContent);

            var animalTable = cut.Find("[data-testid='trace-animals-table']").TextContent;
            Assert.Contains("Room A (ROOM-A)", animalTable);
            Assert.Contains("2026-04-16 06:00 UTC to 2026-04-16 18:00 UTC", animalTable);
            Assert.Contains("Kennel 1 (KEN-1) stay 2026-04-16 06:00 UTC to Open stay", animalTable);
            Assert.Contains("Same location", cut.Find("[data-testid='trace-animal-reason-77-SameLocation']").TextContent);
            Assert.Contains("Adjacent", cut.Find("[data-testid='trace-animal-reason-77-Adjacent']").TextContent);
        });
    }

    private void SetReadOnlyUser() =>
        _authenticationStateProvider.SetUser("readonly-user", KennelTraceRoles.ReadOnly);

    private static void SelectProfileAndAnimal(IRenderedComponent<ContactTrace> cut)
    {
        cut.Find("[data-testid='trace-disease-select']").Change("41");
        cut.Find("[data-testid='trace-source-animal']").Input("A-1 - Milo");
    }

    private static DateTime ParseDateTimeLocal(string? value) =>
        DateTime.ParseExact(value!, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None);

    private static void ActivateTab(IRenderedComponent<ContactTrace> cut, string tabLabel)
    {
        var tab = cut.FindAll("[role='tab']")
            .First(element => string.Equals(element.TextContent.Trim(), tabLabel, StringComparison.Ordinal));
        tab.Click();
    }

    private static ContactTraceResult CreateSuccessResult()
    {
        var overlap = new ImpactedAnimalStayOverlap(
            sourceStayId: 9001,
            sourceLocationId: 101,
            sourceStartUtc: new DateTime(2026, 4, 16, 0, 0, 0, DateTimeKind.Utc),
            sourceEndUtc: new DateTime(2026, 4, 17, 0, 0, 0, DateTimeKind.Utc),
            overlappingStayId: 9100,
            stayLocationId: 102,
            stayStartUtc: new DateTime(2026, 4, 16, 6, 0, 0, DateTimeKind.Utc),
            stayEndUtc: null,
            overlapStartUtc: new DateTime(2026, 4, 16, 6, 0, 0, DateTimeKind.Utc),
            overlapEndUtc: new DateTime(2026, 4, 16, 18, 0, 0, DateTimeKind.Utc));

        var sameLocationReason = new ImpactedLocationReason(
            TraceReasonCode.SameLocation,
            sourceLocationId: 101,
            sourceStayId: 9001);

        var adjacentReason = new ImpactedLocationReason(
            TraceReasonCode.Adjacent,
            sourceLocationId: 101,
            sourceStayId: 9001,
            traversalDepth: 1,
            viaLinkType: LinkType.AdjacentRight);

        return new ContactTraceResult(
            diseaseTraceProfileId: 41,
            sourceStayIds: [9001],
            impactedLocations:
            [
                new ImpactedLocationResult(101, [TraceReasonCode.SameLocation]),
                new ImpactedLocationResult(
                    locationId: 102,
                    reasonCodes: [TraceReasonCode.Adjacent],
                    matchKind: ImpactedLocationMatchKind.ScopedLocation,
                    scopeLocationId: 101,
                    traversalDepth: 1,
                    viaLinkType: LinkType.AdjacentRight)
            ],
            impactedAnimals:
            [
                new ImpactedAnimalResult(77, new AnimalCode("A-77"), "Luna", 101, [overlap], [sameLocationReason, adjacentReason])
            ]);
    }

    private sealed class FakeAnimalReadService : IAnimalReadService
    {
        public IReadOnlyList<AnimalLookupRow> LookupResults { get; set; } = [];

        public Task<IReadOnlyList<AnimalLookupRow>> LookupAnimalsAsync(string? searchText, CancellationToken cancellationToken = default) =>
            Task.FromResult(LookupResults);

        public Task<IReadOnlyList<AnimalMoveLocationOption>> ListMoveLocationsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AnimalMoveLocationOption>>([]);

        public Task<AnimalDetailResult?> GetAnimalDetailAsync(int animalId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AnimalDetailResult?>(null);
    }

    private sealed class FakeContactTraceService : IContactTraceService
    {
        public List<ContactTraceRequest> Requests { get; } = [];

        public ContactTraceResult Result { get; set; } = CreateSuccessResult();

        public Exception? ExceptionToThrow { get; set; }

        public TaskCompletionSource<ContactTraceResult>? PendingResult { get; set; }

        public Task<ContactTraceResult> RunAsync(ContactTraceRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);

            if (PendingResult is not null)
            {
                return PendingResult.Task;
            }

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(Result);
        }
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
