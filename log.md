# Checkpoint Log

## 2026-04-12 13:40:40 -07:00

Implemented the MVP domain vocabulary and natural-key slice in `KennelTrace.Domain` based on `docs/domain-model.md` and `docs/dev-plan.md`. This added the canonical `LocationType` and `LinkType` enums, shared validation and natural-key primitives, and core entity shells for `Facility`, `Location`, `LocationLink`, `Animal`, `MovementEvent`, `Disease`, `DiseaseTraceProfile`, `ImportBatch`, and `ImportIssue`. The constructors and rule helpers make the current business rules explicit, including kennel parent requirements, same-facility containment, adjacency vs topology endpoint validation, non-negative grid values, and UTC-based movement interval validation.

Replaced the placeholder smoke test with focused unit tests for the invariants added in this slice. The tests cover natural-key validation, kennel containment rules, link validation and inverse mapping, half-open movement interval behavior, trace profile link filtering, and import issue validation. Verified the slice with `dotnet build src/KennelTrace.Domain/KennelTrace.Domain.csproj` and `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj`, both of which passed.

## 2026-04-12 14:30:44 -07:00

Implemented the initial SQL Server persistence layer with EF Core across the documented MVP schema in `docs/data-model.sql.md`. This refit the scaffolded domain entities to the SQL shape, added the `KennelTraceDbContext`, SQL Server DI and design-time factory support, and mapped `Facilities`, `Locations`, `LocationLinks`, `Animals`, `MovementEvents`, `Diseases`, `DiseaseTraceProfiles`, `DiseaseTraceProfileTopologyLinkTypes`, `ImportBatches`, and `ImportIssues` with the expected check constraints, composite same-facility foreign keys, and filtered unique indexes. The movement model still preserves half-open interval semantics for overlap handling, while the database now enforces one open stay per animal and the documented active uniqueness rules for kennel grid positions and directed links.

Created the initial EF Core migration and added SQL Server integration coverage that applies migrations to a real SQL Server database, verifies the database can be created from migrations, and exercises the key persistence constraints. The integration tests cover valid end-to-end persistence, active kennel grid uniqueness, active directed link uniqueness with inactive historical replacement, and current-stay enforcement alongside half-open handoff behavior. Verified the slice with `dotnet build KennelTrace.sln`, `dotnet ef migrations add InitialSqlServerPersistence --project src/KennelTrace.Infrastructure/KennelTrace.Infrastructure.csproj --startup-project src/KennelTrace.Web/KennelTrace.Web.csproj --output-dir Persistence/Migrations`, and `dotnet test KennelTrace.sln`, all of which completed successfully in the current repository.

## 2026-04-12 15:39:25 -07:00

Implemented the import pipeline in validate-only mode first, aligned to `docs/import-and-migration.md`. This added import DTOs for `Facilities`, `Rooms`, `Kennels`, and `LocationLinks`, an `.xlsx` workbook reader that parses the canonical sheets directly from OpenXML workbook parts, and a validation service that checks required sheets, exact headers, natural-key duplicates, parent references, cycle risks, kennel grid rules, link endpoint/type rules, inverse-link conflicts, and cross-facility references. The validator now produces explicit row-level `Error` and `Warning` records and a readable plain-text summary intended for technical operators reviewing import output.

Added validate-only batch logging without committing domain changes. The new import service computes a source file hash, builds an `ImportBatch` summary, and records `ImportIssue` rows through an EF-backed logger while leaving facilities, locations, kennels, and links untouched. Registered the import services in DI and added fixture-driven tests for the provided valid, warnings-only, invalid-row, missing-sheet, and bad-header workbooks under `tests/KennelTrace.Tests/import_fixtures`. Verified the slice with `dotnet test KennelTrace.sln`, which passed in the current repository.

## 2026-04-12 17:05:00 -07:00

Extended the import pipeline with commit mode for the four-tab pilot workbook shape: `Facilities`, `Rooms`, `Kennels`, and `LocationLinks`. Commit mode now validates first, persists `ImportBatch` and `ImportIssue` rows, upserts facilities by `FacilityCode`, upserts rooms and kennels as `Location` records by natural key within the facility, and reconciles facility location links deterministically with `CreateInverse` expansion and inactive-history-friendly replacement behavior.

Added SQL Server integration coverage for validate-only issue persistence, successful clean workbook commit, and idempotent rerun behavior using the pilot fixture `PHX_MAIN_Layout_20260412.xlsx`. Verified the change with `dotnet build KennelTrace.sln /p:UseSharedCompilation=false` and `dotnet test KennelTrace.sln /p:UseSharedCompilation=false`, both of which passed in the current repository.

## 2026-04-12 16:32:33 -07:00

Added MudBlazor to `KennelTrace.Web` using the manual installation path. This registered `AddMudServices()`, added the MudBlazor CSS and JS assets in the app root, placed the required theme/popover/dialog/snackbar providers in the main layout, and introduced a single interactive `/mudblazor-test` page with a `MudButton` and `MudTable` plus a nav link to reach it.

Verified the integration with `dotnet build KennelTrace.sln` and a local `dotnet run --project src\KennelTrace.Web\KennelTrace.Web.csproj --launch-profile https --no-build` smoke test. The app started successfully, `/_content/MudBlazor/MudBlazor.min.css` returned `200`, and the test page rendered the expected MudBlazor markup.

## 2026-04-12 19:38:00 -07:00

Split UI testing into dedicated projects by adding `tests/KennelTrace.Web.Tests` for `bUnit` component coverage and `tests/KennelTrace.PlaywrightTests` for Playwright browser automation, while leaving `tests/KennelTrace.Tests` focused on domain, import, and persistence tests. Added starter smoke tests for the Blazor nav menu and the home page, updated `README.md` and `AGENTS.md` with the new test and Playwright install commands, and installed the Playwright browser bundle with Windows PowerShell.

Verified the change with `dotnet restore KennelTrace.sln`, `dotnet build KennelTrace.sln`, and `dotnet test KennelTrace.sln`, all of which passed. The Playwright smoke test remains intentionally skipped until `KENNELTRACE_BASE_URL` is pointed at a running `KennelTrace.Web` instance.

## 2026-04-12 19:58:36 -07:00

Implemented the read-only facility map query slice for the upcoming facility/room/kennel map UI. This added a single EF Core-backed `FacilityMapReadService` plus page-focused DTOs for facility selector options, room selector options, room map results, location detail/summary data, and explicit stored link details under `src/KennelTrace.Infrastructure/Features/Facilities/FacilityMap`. The query shape is scoped to the selected facility and room, returns placed child locations separately from unplaced child locations, preserves stable grid ordering for placed items, exposes only persisted `LocationLink` data, and derives current occupancy counts strictly from open/current `MovementEvent` rows without inferring anything from grid coordinates.

Added SQL Server integration coverage in `tests/KennelTrace.Tests/FacilityMapReadServiceTests.cs` for the required page behaviors: facility listing, facility-scoped room filtering, placed versus unplaced room map results, stable placed-location ordering, current occupancy counts from open stays, and stored-link-only exposure. Registered the new read service in DI and verified the slice with `dotnet build KennelTrace.sln` and `dotnet test KennelTrace.sln`, both of which passed in the current repository.

## 2026-04-12 20:10:00 -07:00

Implemented milestone 7A of the read-only facility map UI in `KennelTrace.Web` with a new `/facility-map` page that consumes the existing `FacilityMapReadService` workflow through `ListFacilitiesAsync()`, `ListRoomsAsync(facilityId)`, and `GetRoomMapAsync(facilityId, roomLocationId)`. The page now loads facilities on first render, refreshes room options when the selected facility changes, loads `RoomMapResult` for the selected room, renders `PlacedLocations` and `UnplacedLocations` separately, and updates a selected-location detail panel from the existing room-map data including explicit stored `Links`. The UI stays intentionally simple for this slice and does not infer adjacency or any other trace relationship from grid coordinates.

Added a small `IFacilityMapReadService` abstraction so the existing concrete read service remains the authoritative runtime path while the bUnit tests can inject deterministic fakes without introducing a second query architecture. Added focused component coverage in `tests/KennelTrace.Web.Tests/FacilityMapPageTests.cs` for successful page render, facility loading, facility-driven room refresh, room-map loading, location-detail selection, empty-room handling, and separate unplaced-location rendering, plus a nav-menu assertion for the new route. Verified the slice with `dotnet build KennelTrace.sln`, `dotnet test tests/KennelTrace.Web.Tests/KennelTrace.Web.Tests.csproj`, and `dotnet test KennelTrace.sln`; all passed in the current repository, with the existing Playwright smoke test still intentionally skipped.

## 2026-04-12 21:19:09 -07:00

Implemented milestone 7B for the read-only facility map UI by upgrading `FacilityMap.razor` from the temporary list rendering to a clearer room-oriented visual layout while keeping the existing route, nav entry, `IFacilityMapReadService` seam, DTOs, and stable `location-{LocationId}` test IDs intact. Placed child locations now render as selectable visual tiles grouped by stored row and column placement, the selected state is visually obvious, the detail panel remains visible and updates from the already loaded room-map DTOs, unplaced locations remain in a separate section, and the page still relies only on the authoritative stored `Links` collection without inferring adjacency or connectivity from coordinates.

Added scoped page styling plus focused test updates in `tests/KennelTrace.Web.Tests` for the new layout hooks and selected-state behavior, and added one Playwright happy-path smoke test in `tests/KennelTrace.PlaywrightTests` that hosts the app with a fake `IFacilityMapReadService`, navigates to `/facility-map`, selects a facility and room, verifies the placed-locations area, clicks a stable location locator, and confirms the detail panel updates. Also enabled the required interactive server render-mode wiring for the facility map/browser flow and added a minimal `Program` partial for `WebApplicationFactory`. Verified the slice with `dotnet build KennelTrace.sln` and `dotnet test KennelTrace.sln`, both of which passed in the current repository; the older environment-dependent `HomePageSmokeTests` case remains intentionally skipped.

## 2026-04-12 21:55:50 -07:00

Added a Development-only database bootstrap path so the local web app is usable without a separate manual EF/database step. On startup, `KennelTrace.Web` now calls a new development setup helper that applies pending SQL Server migrations and seeds one small facility-map dataset if facility `DEV-PHX` does not already exist. The seed is intentionally minimal and idempotent: one facility, one room, one hallway, several kennels including an unplaced kennel, explicit stored room and kennel links, and one current occupant via an open `MovementEvent`. Non-development environments still do not auto-migrate or auto-seed.

Updated `README.md` to document the new local-development startup behavior and kept the change scoped to local convenience rather than canonical production behavior. Verified the change with `dotnet build KennelTrace.sln` and `dotnet test tests/KennelTrace.Web.Tests/KennelTrace.Web.Tests.csproj --no-build`. A first `dotnet test` attempt after the build hit a transient Roslyn compiler file lock from `VBCSCompiler`; rerunning the test project with `--no-build` passed cleanly.

## 2026-04-15 10:30:00 -07:00

Implemented milestone 8A with the first admin-only maintenance slice for facilities plus the minimum authorization foundation the current repo can support cleanly. The web app now has an explicit role catalog for `ReadOnly` and `Admin`, an `AdminOnly` policy for protected save workflows, a development-only simulated authenticated principal configured through `appsettings.Development.json`, `AuthorizeRouteView` route protection, an admin-only `/admin/layout` page, and a nav entry that only renders for admins. Facility maintenance stays form-based and limited to facilities in this slice: admins can create or edit `FacilityCode`, `Name`, `TimeZoneId`, `IsActive`, and `Notes`, with active/inactive handling instead of delete and friendly duplicate-code validation.

The protected facility write path now runs through a dedicated `FacilityAdminService` that checks admin authorization server-side before saving, preserves `CreatedUtc`, updates `ModifiedUtc`, and rejects non-admin write attempts even if the UI path is bypassed. Added bUnit coverage for admin-vs-read-only nav and route behavior plus SQL-backed integration tests for facility create/update, duplicate `FacilityCode`, and non-admin write rejection. Commands run in this repo for this slice were:

- `dotnet build KennelTrace.sln`
- `dotnet test tests/KennelTrace.Web.Tests/KennelTrace.Web.Tests.csproj`
- `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj --no-build`
- `git status --short`

Verification result in this shell:

- `dotnet build KennelTrace.sln` passed.
- `dotnet test tests/KennelTrace.Web.Tests/KennelTrace.Web.Tests.csproj` passed.
- `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj --no-build` failed because the repository's SQL Server-backed integration suites could not connect to a local SQL Server instance using the default `KENNELTRACE_TEST_SQLSERVER`/`localhost` configuration on this machine.

## 2026-04-15 12:05:00 -07:00

Implemented milestone 8B by extending the existing admin-only `/admin/layout` route into a simple containment maintenance screen rather than adding a competing editor. The page now keeps the 8A facility management area and adds a second location-management section with a facility selector, a browseable grouped location tree, and a selected-location form for `LocationType`, `LocationCode`, `Name`, `ParentLocationId`, `DisplayOrder`, `IsActive`, and `Notes`. Inactive locations are called out directly in the browser and form, creation stays form-based, and delete remains intentionally deferred in favor of history-friendly deactivation.

Added a dedicated `LocationAdminService` for server-side authorized writes plus explicit containment validation. The write path now enforces same-facility parents, self-parent rejection, cycle detection, room-like-only containment parents for kennels, allowed parent/child combinations, and `LocationCode` uniqueness within a facility. Shared containment helpers now live in `LocationTypeRules`, and the `Location` aggregate has an explicit update method so write behavior is not scattered through Razor handlers. Added SQL-backed tests for the rule-heavy save logic and bUnit coverage for facility switching, location selection, create/edit save flows, and validation message display.

Commands run in this repo for this slice were:

- `git status --short`
- `dotnet build KennelTrace.sln`
- `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj`
- `dotnet test tests/KennelTrace.Web.Tests/KennelTrace.Web.Tests.csproj`
- `git diff -- src\KennelTrace.Domain\Features\Locations\Location.cs src\KennelTrace.Domain\Features\Locations\LocationTypeRules.cs src\KennelTrace.Web\Program.cs src\KennelTrace.Web\Components\Pages\AdminLayout.razor src\KennelTrace.Web\Features\Locations\Admin\LocationAdminService.cs tests\KennelTrace.Tests\LocationAdminServiceTests.cs tests\KennelTrace.Web.Tests\AdminLayoutPageTests.cs tests\KennelTrace.Web.Tests\AdminLayoutRouteTests.cs`

Verification result in this shell:

- `dotnet build KennelTrace.sln` passed.
- `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj` passed with 35 tests.
- `dotnet test tests/KennelTrace.Web.Tests/KennelTrace.Web.Tests.csproj` passed with 17 tests.

## 2026-04-15 13:40:00 -07:00

Implemented milestone 8C by extending the existing admin-only `/admin/layout` workflow with kennel placement editing instead of creating a separate editor surface. Kennel location saves now carry `GridRow`, `GridColumn`, `StackLevel`, and `DisplayOrder` through the existing `LocationAdminService`, with server-side validation for paired row/column input, non-negative grid values, kennel-only grid editing, and active same-room kennel placement collisions on `(ParentLocationId, GridRow, GridColumn, StackLevel)`. The selected-location form now exposes the grid fields only for kennel rows, while selected room-like parents (`Room`, `Medical`, `Isolation`, `Intake`) show a room-scoped kennel placement table that keeps unplaced kennels editable without forcing coordinates.

Reused the existing read-side room-map seam by injecting `IFacilityMapReadService` into the admin page for a simple placed/unplaced preview that reflects stored data only. This keeps the grid as a display and maintenance aid and does not introduce any adjacency or trace inference from coordinates. Added SQL-backed integration coverage for paired grid validation, negative values, and active placed-kennel collisions, plus bUnit coverage for room-scoped placement editing and placed versus unplaced admin preview rendering. Commands run in this repo for this slice were:

- `dotnet build KennelTrace.sln`
- `dotnet test tests/KennelTrace.Web.Tests/KennelTrace.Web.Tests.csproj --no-build`
- `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj --no-build`
- `dotnet test KennelTrace.sln --no-build`

Verification result in this shell:

- `dotnet build KennelTrace.sln` passed.
- `dotnet test tests/KennelTrace.Web.Tests/KennelTrace.Web.Tests.csproj --no-build` passed with 19 tests.
- `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj --no-build` passed with 38 tests.
- `dotnet test KennelTrace.sln --no-build` passed; the existing Playwright home-page smoke test remained intentionally skipped.

## 2026-04-15 15:05:00 -07:00

Implemented milestone 8D by adding admin management for explicit adjacency and topology links inside the existing authorized `/admin/layout` area. The selected-location panel now shows active outgoing and incoming link tables, and admins can add or remove links through simple dialog flows without changing the existing read-only facility map behavior. Link writes now run through a dedicated `LocationLinkAdminService` with explicit reciprocal-row handling: creating `AdjacentLeft/Right`, `AdjacentAbove/Below`, `AdjacentOther`, `Connected`, `Airflow`, or `TransportPath` adds or reactivates both directed rows with `SourceType = Manual`, while remove deactivates both directed rows consistently instead of hard-deleting them.

Extended the location admin read model so the admin page can list active stored links for the selected facility, added server-side validation for self-links, cross-facility links, duplicate active directed links, adjacency kennel-only endpoints, and default topology non-kennel-space endpoints with an explicit admin-visible override path for unusual topology endpoints. Added SQL-backed integration coverage for reciprocal-row creation, reciprocal deactivation on remove, duplicate rejection, endpoint-family validation, cross-facility rejection, and one end-to-end integration that proves an admin-added link appears through the existing read-only room-map data path. Added bUnit coverage for outgoing/incoming link tables plus add/remove dialog flows. Commands run in this repo for this slice were:

- `dotnet build KennelTrace.sln`
- `dotnet test tests/KennelTrace.Web.Tests/KennelTrace.Web.Tests.csproj --no-build`
- `dotnet test tests/KennelTrace.PlaywrightTests/KennelTrace.PlaywrightTests.csproj --no-build`
- `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj --no-build`
- `dotnet test KennelTrace.sln --no-build`
- `git status --short`
- `git diff -- src\KennelTrace.Domain\Features\Locations\LocationLink.cs src\KennelTrace.Domain\Features\Locations\LocationTypeRules.cs src\KennelTrace.Web\Program.cs src\KennelTrace.Web\Features\Locations\Admin\LocationAdminService.cs src\KennelTrace.Web\Features\Locations\Admin\LocationLinkAdminService.cs src\KennelTrace.Web\Components\Pages\AdminLayout.razor tests\KennelTrace.Tests\LocationLinkAdminServiceTests.cs tests\KennelTrace.Web.Tests\AdminLayoutPageTests.cs tests\KennelTrace.Web.Tests\AdminLayoutRouteTests.cs log.md`

Verification result in this shell:

- `dotnet build KennelTrace.sln` passed.
- `dotnet test tests/KennelTrace.Web.Tests/KennelTrace.Web.Tests.csproj --no-build` passed with 22 tests.
- `dotnet test tests/KennelTrace.PlaywrightTests/KennelTrace.PlaywrightTests.csproj --no-build` passed with 1 passing Playwright test and 1 intentionally skipped environment-dependent home-page test.
- `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj --no-build` passed with 44 tests.
- `dotnet test KennelTrace.sln --no-build` passed across all three test projects.

## 2026-04-15 17:35:00 -07:00

Implemented milestone 8E as the admin-layout closeout slice. The existing `/admin/layout` page now gives clearer, low-friction feedback for pilot admins instead of silently relying on refreshed form state alone: facility, location, placement, and link saves now show explicit success messages; empty states for facilities, locations, link management, kennel placement, preview data, and empty link-target lists now explain the next sensible admin action; and the page code was lightly cleaned up by centralizing repeated feedback-reset logic instead of scattering the same state clearing in each handler.

Replaced the earlier read-only-only Playwright smoke with one realistic admin-to-read-only workflow backed by a shared in-memory test store. The browser test now signs in as an admin, opens `/admin/layout`, creates a room, creates a child kennel, sets kennel grid placement from the room placement table, creates a topology link from the room to a hallway, then navigates to `/facility-map` and verifies that the new room, placed kennel, and stored link are visible through the read-only map path. Added bUnit coverage for the new success feedback and the more actionable empty-state copy on the admin page.

Commands run in this repo for this slice were:

- `dotnet build KennelTrace.sln`
- `dotnet test tests/KennelTrace.Web.Tests/KennelTrace.Web.Tests.csproj --no-restore`
- `dotnet build KennelTrace.sln /p:UseSharedCompilation=false`
- `dotnet build tests/KennelTrace.PlaywrightTests/KennelTrace.PlaywrightTests.csproj /p:UseSharedCompilation=false --no-restore`
- `dotnet test tests/KennelTrace.PlaywrightTests/KennelTrace.PlaywrightTests.csproj --no-build`
- `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj --no-build`
- `git status --short`

Verification result in this shell:

- `dotnet build KennelTrace.sln` first failed because `KennelTrace.Domain.dll` was transiently locked by `VBCSCompiler`/Defender.
- `dotnet test tests/KennelTrace.Web.Tests/KennelTrace.Web.Tests.csproj --no-restore` passed with 23 tests.
- `dotnet build KennelTrace.sln /p:UseSharedCompilation=false` exposed a missing `ILoggerFactory` import in the new Playwright test and also hit testhost copy-lock retries in `tests/KennelTrace.Tests`; it was not used as the final verification step.
- `dotnet build tests/KennelTrace.PlaywrightTests/KennelTrace.PlaywrightTests.csproj /p:UseSharedCompilation=false --no-restore` passed and compiled the new admin smoke test.
- `dotnet test tests/KennelTrace.PlaywrightTests/KennelTrace.PlaywrightTests.csproj --no-build` passed with 1 active Playwright smoke test and 1 intentionally skipped environment-dependent home-page test.
- `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj --no-build` timed out in this shell and was not used as a success signal for this slice.

## 2026-04-17 10:45:00 -07:00

Implemented the Step 9 read-side animal lookup/detail slice under `src/KennelTrace.Infrastructure/Features/Animals/AnimalRecords`. This added an EF Core-backed `AnimalReadService` plus a small `IAnimalReadService` seam mirroring the existing facility-map query pattern, page-focused DTOs for lookup rows, detail results, current placement summaries, and movement history rows, and DI registration through the existing SQL Server service extension. Animal detail now derives current placement strictly from the open `MovementEvent` row, returns current facility/location from stored data, includes room context when available, keeps movement history in deterministic reverse chronological order using `StartUtc DESC` plus `MovementEventId DESC`, and still shows historical rows after related locations are later marked inactive.

Added SQL-backed integration coverage in `tests/KennelTrace.Tests/AnimalReadServiceTests.cs` for lookup by `AnimalNumber`, lookup by `Name`, detail data with no current placement, current placement from an open stay, reverse-chronological history ordering, and inactive-location history visibility. The simple lookup path remains EF-backed but filters in memory after materializing `Animals`, because EF Core translation against the existing `AnimalCode` value-converted property produced provider coercion errors when trying to query `AnimalNumber` text directly.

Commands actually run in this shell for this slice:

- `git status --short`
- `dotnet build KennelTrace.sln`
- `dotnet build KennelTrace.sln`
- `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj`
- `dotnet build KennelTrace.sln`
- `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj`
- `dotnet build KennelTrace.sln`
- `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj`
- `dotnet test KennelTrace.sln`

Verification result in this shell:

- The final `dotnet build KennelTrace.sln` passed.
- Earlier `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj` runs failed while fixing `AnimalReadService.LookupAnimalsAsync()` translation/coercion issues around the `AnimalCode` value converter.
- The final `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj` passed with 50 tests.
- `dotnet test KennelTrace.sln` did not pass because the existing Playwright test `KennelTrace.PlaywrightTests.AdminLayoutSmokeTests.Admin_Layout_HappyPath_Creates_Room_And_Kennel_Then_Shows_Them_On_Facility_Map` failed waiting for `data-testid="location-save-success"` to contain `Surgery Prep saved.`.
- In that same solution test run, `tests/KennelTrace.Web.Tests` passed with 23 tests, `tests/KennelTrace.Tests` passed with 50 tests, and `tests/KennelTrace.PlaywrightTests` had 1 failed and 1 skipped test.

## 2026-04-17 09:22:50 -07:00

Fixed the admin-layout Playwright regression that was failing after the latest slice. The root cause was interactive Blazor prerendered markup: the browser smoke could type into the prerendered admin form before the live `/_blazor` circuit was attached, so the first save request reached the server with empty location values and the page fell back to the generic location-save error path.

The fix disables prerendering for the routed app shell in `src/KennelTrace.Web/Components/App.razor`, updates the admin form inputs in `src/KennelTrace.Web/Components/Pages/AdminLayout.razor` to bind on `oninput`, hardens the Playwright smoke to wait for the Blazor websocket and prove the page is interactive before starting the create flow, and updates the affected bUnit tests to dispatch `Input(...)` for those fields.

Commands run in this repo for the fix were:

- `dotnet test tests/KennelTrace.PlaywrightTests/KennelTrace.PlaywrightTests.csproj --filter "FullyQualifiedName~Admin_Layout_HappyPath_Creates_Room_And_Kennel_Then_Shows_Them_On_Facility_Map"`
- `dotnet test tests/KennelTrace.PlaywrightTests/KennelTrace.PlaywrightTests.csproj`
- `dotnet test tests/KennelTrace.Web.Tests/KennelTrace.Web.Tests.csproj`
- `dotnet test KennelTrace.sln`

Verification result in this shell:

- The focused admin Playwright smoke passed.
- `tests/KennelTrace.PlaywrightTests` passed with 1 active test passing and 1 intentionally skipped environment-dependent test.
- `tests/KennelTrace.Web.Tests` passed with 23 tests.
- `dotnet test KennelTrace.sln` passed across all three test projects.

## 2026-04-17 09:51:39 -07:00

Implemented the Step 9 read-only animal UI slice in `KennelTrace.Web` using the existing `IAnimalReadService` seam instead of duplicating query logic in Razor pages. Added an authorized `/animals` lookup page with a simple search box, honest initial and no-results states, a table-first result list, and navigation into read-only detail pages. Added an authorized `/animals/{animalId:int}` detail page with a minimal summary card, clear current placement/facility/location display, reverse-chronological movement history, and explicit `Current` labeling for open stays. The nav menu now includes an `Animals` entry for the same read-only/admin audience as the rest of the operational lookup UI.

Added focused bUnit coverage in `tests/KennelTrace.Web.Tests/AnimalPagesTests.cs` for the requested scenarios: `/animals` initial render, result rendering, empty-result state, detail render, current placement display, open-stay/current labeling, movement history table rendering, and route authorization behavior. The tests reuse the repo’s existing fake injected-service pattern and add a small test-only `IKeyInterceptorService` stub so MudBlazor components render cleanly under bUnit without changing runtime architecture.

Commands actually run in this shell for this slice:

- `dotnet build KennelTrace.sln`
- `dotnet build KennelTrace.sln`
- `dotnet test tests/KennelTrace.Web.Tests/KennelTrace.Web.Tests.csproj`
- `dotnet test tests/KennelTrace.Web.Tests/KennelTrace.Web.Tests.csproj`
- `dotnet test tests/KennelTrace.Web.Tests/KennelTrace.Web.Tests.csproj`
- `dotnet test KennelTrace.sln`
- `dotnet test KennelTrace.sln`
- `Get-Date -Format "yyyy-MM-dd HH:mm:ss zzz"`

Verification result in this shell:

- The first `dotnet build KennelTrace.sln` failed while fixing new bUnit API usage and a MudBlazor analyzer warning in the new animals page.
- The second `dotnet build KennelTrace.sln` passed.
- The first two `dotnet test tests/KennelTrace.Web.Tests/KennelTrace.Web.Tests.csproj` runs failed while configuring MudBlazor JS interop and a test-local key interceptor stub for the new bUnit coverage.
- The final `dotnet test tests/KennelTrace.Web.Tests/KennelTrace.Web.Tests.csproj` passed with 32 tests.
- The first `dotnet test KennelTrace.sln` run timed out in this shell before completion.
- The final `dotnet test KennelTrace.sln` passed across all three test projects, with `tests/KennelTrace.PlaywrightTests` reporting 1 passing test and 1 intentionally skipped environment-dependent test, `tests/KennelTrace.Web.Tests` reporting 32 passing tests, and `tests/KennelTrace.Tests` reporting 50 passing tests.

## 2026-04-17 10:16:25 -07:00

Implemented the admin-only animal maintenance slice at `/admin/animals` using the same server-authorized admin-service pattern already established for `/admin/layout`. The new workflow stays intentionally simple and form-based: admins can search/browse animals by number or name through the existing `IAnimalReadService`, select an existing record into an edit form, create a new record, and deactivate an existing record by clearing `IsActive` instead of deleting it. The explicit `AnimalAdminService` now owns all animal writes server-side, enforces admin authorization plus required/unique `AnimalNumber`, keeps the model limited to the documented MVP fields, and uses the existing `Animals` table shape without schema changes or migrations.

Added SQL-backed integration tests in `tests/KennelTrace.Tests/AnimalAdminServiceTests.cs` for create, update, unique `AnimalNumber`, and deactivate-instead-of-delete behavior. Added bUnit coverage in `tests/KennelTrace.Web.Tests/AdminAnimalsPageTests.cs` and `tests/KennelTrace.Web.Tests/AdminAnimalsRouteTests.cs` for page render, select/create/edit flows, validation message display, and admin-only route protection. The admin nav now includes `Admin Animals` for admins only, and `NavMenuTests` was updated to verify the new link visibility.

Commands actually run in this shell for this slice:

- `dotnet build KennelTrace.sln`
- `dotnet build KennelTrace.sln`
- `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj`
- `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj`
- `dotnet test tests/KennelTrace.Web.Tests/KennelTrace.Web.Tests.csproj`
- `dotnet test tests/KennelTrace.Web.Tests/KennelTrace.Web.Tests.csproj`
- `dotnet test KennelTrace.sln`
- `Get-Date -Format "yyyy-MM-dd HH:mm:ss zzz"`

Verification result in this shell:

- The first `dotnet build KennelTrace.sln` failed because `AdminAnimals.razor` used an incompatible `type="date"` bind on a `string` field; the field was changed to explicit `value`/`oninput` handling.
- The second `dotnet build KennelTrace.sln` passed.
- The first `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj` run timed out before completion in this shell.
- The second `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj` passed with 54 tests.
- The first `dotnet test tests/KennelTrace.Web.Tests/KennelTrace.Web.Tests.csproj` run failed because two new bUnit assertions targeted the form-body test container instead of the card header text.
- The second `dotnet test tests/KennelTrace.Web.Tests/KennelTrace.Web.Tests.csproj` passed with 39 tests.
- `dotnet test KennelTrace.sln` passed across all three test projects, with `tests/KennelTrace.Tests` reporting 54 passing tests, `tests/KennelTrace.Web.Tests` reporting 39 passing tests, and `tests/KennelTrace.PlaywrightTests` reporting 1 passing test plus 1 intentionally skipped environment-dependent test.

## 2026-04-17 10:42:53 -07:00

Implemented the Step 9 movement-write slice without adding UI. The new `AnimalMovementAdminService` under `src/KennelTrace.Web/Features/Animals/Admin` follows the repo's existing admin-service pattern: it enforces `AdminOnly` authorization server-side, accepts an explicit stay-recording request model (`AnimalId`, `LocationId`, `StartUtc`, optional `EndUtc`, optional `MovementReason`, optional `Notes`), uses interval-based `MovementEvent` rows with half-open semantics, and records `RecordedByUserId` from `ClaimTypes.NameIdentifier` when available with a fallback to `Identity.Name`. The write path stays manual-entry focused and uses the existing schema and filtered unique index with no migration changes.

The movement write logic now runs inside a SQL transaction and follows the documented pattern for the same animal: read the animal's existing stays in-transaction, reject overlap using the half-open predicate, close the current open stay at the new stay's `StartUtc` when the request represents a move, insert the new stay, and commit only after re-checking that no overlap exists and at most one open stay remains. Added SQL-backed integration coverage in `tests/KennelTrace.Tests/AnimalMovementAdminServiceTests.cs` for first open stay creation, open-stay handoff on move, same-timestamp handoff, overlapping historical rejection, second-open prevention, closed-stay-only history, cross-facility consecutive moves, `RecordedByUserId` capture, and non-admin rejection.

Commands actually run in this shell for this slice:

- `dotnet build KennelTrace.sln`
- `dotnet build KennelTrace.sln`
- `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj --filter "FullyQualifiedName~AnimalMovementAdminServiceTests"`
- `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj`
- `dotnet test KennelTrace.sln`
- `Get-Date -Format "yyyy-MM-dd HH:mm:ss zzz"`
- `git status --short`

Verification result in this shell:

- The first `dotnet build KennelTrace.sln` failed because `AnimalMovementAdminService.cs` was missing the `KennelTrace.Domain.Common` import for `SourceType`.
- The second `dotnet build KennelTrace.sln` passed.
- `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj --filter "FullyQualifiedName~AnimalMovementAdminServiceTests"` passed with 9 tests.
- `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj` passed with 63 tests.
- `dotnet test KennelTrace.sln` passed across all three test projects, with `tests/KennelTrace.Tests` reporting 63 passing tests, `tests/KennelTrace.Web.Tests` reporting 39 passing tests, and `tests/KennelTrace.PlaywrightTests` reporting 1 passing test plus 1 intentionally skipped environment-dependent test.

## 2026-04-17 11:11:40 -07:00

Implemented the Step 9 admin move-entry UI slice without starting trace UI work. The read-only animal detail page at `src/KennelTrace.Web/Components/Pages/AnimalDetail.razor` now keeps its existing summary/history role and adds an admin-only `Record move` action that links to the new admin-only workflow at `/admin/animals/{animalId:int}/move`. The new `AdminAnimalMove.razor` page stays intentionally form-based: it loads the selected animal through the existing `IAnimalReadService`, shows a basic summary plus current placement, loads persisted location options through a small addition to the existing animal read seam, accepts `LocationId`, `StartUtc`, optional `EndUtc`, `MovementReason`, and `Notes`, and delegates all overlap/open-stay enforcement to the existing `AnimalMovementAdminService` instead of duplicating those rules in Razor.

Added focused automated coverage for the requested workflow. `tests/KennelTrace.Web.Tests` now includes bUnit coverage for admin visibility of the `Record move` action, move-form render, validation/error display from failed saves, successful save navigation back to detail, refreshed detail-state after save, and direct-route authorization for the new admin page. `tests/KennelTrace.Tests/AnimalReadServiceTests.cs` also adds one integration case for the new move-location read model so the admin move form stays backed by persisted data rather than ad hoc UI state.

Commands actually run in this shell for this slice:

- `dotnet build KennelTrace.sln`
- `dotnet test tests/KennelTrace.Web.Tests/KennelTrace.Web.Tests.csproj`
- `dotnet test tests/KennelTrace.Web.Tests/KennelTrace.Web.Tests.csproj`
- `dotnet test tests/KennelTrace.Web.Tests/KennelTrace.Web.Tests.csproj`
- `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj`
- `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj`
- `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj`
- `dotnet test KennelTrace.sln`
- `dotnet build KennelTrace.sln`
- `Get-Date -Format "yyyy-MM-dd HH:mm:ss zzz"`

Verification result in this shell:

- The first `dotnet test tests/KennelTrace.Web.Tests/KennelTrace.Web.Tests.csproj` run failed because the new detail-page `AuthorizeView` required a cascading authentication state in existing bUnit detail tests, and the new detail-refresh test needed MudBlazor test services.
- The second `dotnet test tests/KennelTrace.Web.Tests/KennelTrace.Web.Tests.csproj` run failed during compile because the new test helpers used the wrong bUnit render-helper type and were missing a `KeyboardEventArgs` import.
- The final `dotnet test tests/KennelTrace.Web.Tests/KennelTrace.Web.Tests.csproj` passed with 46 tests.
- The first `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj` run timed out in this shell.
- The second `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj` run failed because the new integration test fixture created a kennel without its required room-like parent.
- The final `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj` passed with 64 tests.
- `dotnet test KennelTrace.sln` passed across all three test projects, with `tests/KennelTrace.Tests` reporting 64 passing tests, `tests/KennelTrace.Web.Tests` reporting 46 passing tests, and `tests/KennelTrace.PlaywrightTests` reporting 1 passing test plus 1 intentionally skipped environment-dependent test.
- The final `dotnet build KennelTrace.sln` passed against the finished code state.

## 2026-04-17 11:35:00 -07:00

Implemented the Step 10A tracing-contract slice without adding UI or database-backed trace orchestration. Added explicit trace request/result contracts under `src/KennelTrace.Domain/Features/Tracing` for `ContactTraceRequest`, `ContactTraceResult`, `ImpactedLocationResult`, `ImpactedAnimalResult`, `TraceReasonCode`, and simple exact-vs-scoped location match metadata so later slices can represent scope-driven hits without reshaping the API. Also added `DiseaseTraceProfileSemantics` as a small pure helper for adjacency enablement/depth, topology enablement/depth, and allowed topology link types only when topology traversal is enabled, plus a single `IContactTraceService` seam under `src/KennelTrace.Infrastructure/Features/Tracing/ContactTracing`.

Added focused unit coverage in `tests/KennelTrace.Tests/ContactTraceContractsTests.cs` for explicit request validation, reason-code requirements, scoped-result metadata, and profile interpretation rules. This slice intentionally treats `DefaultLookbackHours` as profile metadata only and does not use it to override an explicit request window.

Commands actually run in this shell for this slice:

- `dotnet build KennelTrace.sln`
- `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj`
- `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj --no-build`
- `dotnet test KennelTrace.sln --no-build`
- `git status --short`

Verification result in this shell:

- `dotnet build KennelTrace.sln` passed.
- The first `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj` run failed due to a transient `VBCSCompiler` file lock on `KennelTrace.Domain.dll`.
- `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj --no-build` passed with 72 tests.
- `dotnet test KennelTrace.sln --no-build` passed across all three test projects, with `tests/KennelTrace.Tests` reporting 72 passing tests, `tests/KennelTrace.Web.Tests` reporting 46 passing tests, and `tests/KennelTrace.PlaywrightTests` reporting 1 passing test plus 1 intentionally skipped environment-dependent test.

## 2026-04-17 14:01:32 -07:00

Implemented Slice 10B for pure impacted-location graph expansion in `src/KennelTrace.Domain/Features/Tracing` without adding EF querying or UI behavior. Added a pure `ImpactedLocationGraphExpander` plus request/snapshot/result types for resolved source stays and locations, interpreted profile settings, supplied containment/link graph data, and explicit impacted-location reason metadata. The expansion now covers same location, same room from containment, directed adjacency traversal by depth, and topology traversal by allowed link type and depth, while preserving deterministic ordering, explainable scoped-vs-exact matching metadata, and deduplicated multi-path reasons without inferring anything from grid coordinates.

Added focused unit coverage in `tests/KennelTrace.Tests/ImpactedLocationGraphExpanderTests.cs` for containment-derived same-room behavior, adjacency depth 1 vs 2, authored-direction link traversal, topology filtering and reason-code mapping, partial graph snapshots, irregular-grid non-inference, same-location source handling, and deterministic output when multiple reasons reach the same location.

Commands actually run in this shell for this slice:

- `dotnet build KennelTrace.sln`
- `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj`
- `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj`
- `dotnet test KennelTrace.sln`
- `Get-Date -Format "yyyy-MM-dd HH:mm:ss zzz"`

Verification result in this shell:

- `dotnet build KennelTrace.sln` passed.
- The first `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj` run timed out before completion in this shell.
- The second `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj` passed with 80 tests.
- `dotnet test KennelTrace.sln` passed across all three test projects, with `tests/KennelTrace.Tests` reporting 80 passing tests, `tests/KennelTrace.Web.Tests` reporting 46 passing tests, and `tests/KennelTrace.PlaywrightTests` reporting 1 passing test plus 1 intentionally skipped environment-dependent test.
