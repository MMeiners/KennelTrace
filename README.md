# KennelTrace

KennelTrace is an internal kennel management and contact tracing application for shelter staff.

Its first goal is not flashy automation. Its first goal is to replace Word-based layout records and ad hoc notes with a structured, queryable source of truth for facilities, kennels, room relationships, animal movement history, and disease tracing.

## Current status

This repository now contains the initial scaffold for the application alongside the planning and design package.

What is already defined:

- product vision and scope
- MVP and later-phase feature planning
- recommended architecture and engineering standards
- canonical domain model
- draft SQL Server schema
- UI / UX direction
- import and migration approach
- acceptance criteria
- phased development plan
- Blazor Server web host scaffold
- modular monolith solution structure under `src/` and `tests/`
- validated solution-level build and test commands

## Why this project exists

Shelter staff currently maintain important operational knowledge in Word documents and ad hoc notes, including:

- which kennels exist
- which room each kennel belongs to
- which spaces are adjacent
- which spaces are connected by airflow or transport paths
- where animals were housed over time

KennelTrace turns that into structured data that staff can view, maintain, and use for disease/contact investigations.

## MVP scope

The first valuable release should include:

- multiple facilities
- a location registry for rooms, kennels, hallways, isolation, medical spaces, and related areas
- simple kennel map/grid placement
- explicit adjacency and topology links
- read-only facility and kennel map views
- admin editing for locations and links
- animal records and movement history
- disease trace profiles and a working contact trace flow
- admin/script-driven import pipeline
- basic `ReadOnly` and `Admin` authorization

## Recommended technology direction

The planning docs currently recommend:

- ASP.NET Core Blazor Server
- MudBlazor
- EF Core
- SQL Server
- modular monolith architecture
- on-prem / internal deployment posture

## Build and test commands

Validated in this repository:

- `dotnet restore KennelTrace.sln`
- `dotnet build KennelTrace.sln`
- `dotnet test KennelTrace.sln`
- `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj`
- `dotnet test tests/KennelTrace.Web.Tests/KennelTrace.Web.Tests.csproj`
- `dotnet test tests/KennelTrace.PlaywrightTests/KennelTrace.PlaywrightTests.csproj`

Playwright browser setup for the browser test project:

- `dotnet build tests/KennelTrace.PlaywrightTests/KennelTrace.PlaywrightTests.csproj`
- `pwsh .\bin\Debug\net10.0\playwright.ps1 install` from `tests/KennelTrace.PlaywrightTests`
- set `KENNELTRACE_BASE_URL` to a running `KennelTrace.Web` base URL before enabling non-skipped browser tests

If `dotnet` is not on `PATH` in the current shell on this machine, the validated SDK path is `C:\Users\Mark\.dotnet\dotnet.exe`.

## Core modeling rules

These rules should stay consistent across code, schema, imports, and UI:

- The graph is authoritative. The grid is a usability aid.
- One physical place should map to one `Location` row.
- Containment and graph relationships are separate concerns.
- Trace logic must use explicit stored links, not guessed adjacency from grid coordinates.
- Movement history is interval-based using `StartUtc` and `EndUtc`.
- Natural keys matter for import, reconciliation, and safe re-runs.
- Historical data should be preserved; locations are typically deactivated, not deleted.

## Repository roadmap

Recommended solution structure once implementation begins:

```text
docs/
src/
  KennelTrace.Web
  KennelTrace.Domain
  KennelTrace.Infrastructure
tests/
  KennelTrace.Tests
  KennelTrace.Web.Tests
  KennelTrace.PlaywrightTests
```

Feature-oriented folders are preferred inside projects, for example:

```text
Features/
  Facilities/
  Locations/
  Animals/
  Tracing/
  Imports/
```

## Document guide

Start with these docs in this order:

1. `docs/vision.md` — product intent, goals, and non-goals
2. `docs/domain-model.md` — canonical domain vocabulary and business rules
3. `docs/architecture.md` — implementation shape and technology direction
4. `docs/acceptance-criteria.md` — what "done" means for MVP
5. `docs/dev-plan.md` — recommended build order

Additional references:

- `docs/features.md` — MVP, Phase 2, and Phase 3 feature scope
- `docs/data-model.sql.md` — draft SQL Server schema
- `docs/import-and-migration.md` — canonical spreadsheet import and migration plan
- `docs/ui-ux.md` — screen and interaction guidance
- `docs/engineering-standards.md` — code organization and pragmatic implementation rules

## Source-of-truth order

When documents overlap, use this order:

1. acceptance criteria
2. domain model
3. architecture
4. data model
5. development plan
6. supporting docs

Once code exists, implemented behavior and tests should be kept aligned with the acceptance criteria and domain model.

## Recommended implementation order

1. Lock vocabulary, enums, and natural keys.
2. Implement the SQL/EF Core foundation.
3. Build the import validation and batch workflow.
4. Load one pilot facility from real data.
5. Build the read-only facility and kennel map.
6. Add admin maintenance for locations and links.
7. Add animal movement history.
8. Implement disease profiles and contact tracing.
9. Add trace UI.
10. Refine based on real shelter usage.

## Deliberate non-goals for MVP

Do not start by building:

- a full shelter management platform
- a polished drag-and-drop floor-plan editor
- a generic rules engine for every disease
- microservices
- mobile/offline-first support
- a self-service import wizard
- advanced analytics before the data foundation is trusted

## Working principles

- Prioritize readability and maintainability over architectural ceremony.
- Keep business rules explicit and test-covered.
- Avoid generic repository layers unless a concrete problem justifies them.
- Prefer a small number of moving parts.
- Validate the model with one real pilot facility before chasing broad feature depth.

## Next step

The recommended next implementation milestone is to lock the canonical domain vocabulary, location and link catalogs, and natural keys before starting persistence or feature work.
