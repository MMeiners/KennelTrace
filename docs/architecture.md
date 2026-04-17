# Architecture

## Recommended Blazor model

**Recommendation: stay with Blazor Server for MVP.**

This is the right choice for the constraints already defined:

- internal shelter staff application
- on-prem hosting
- SQL Server
- small internal team
- early adoption over architectural purity
- no offline/mobile-first requirement
- strong need for centralized deployment and maintainability

### Why Blazor Server fits

- **Centralized deployment:** updates happen on the server, which is valuable for an internal app with a small team.
- **Simple data access:** the UI can orchestrate against server-side services without adding a separate client API surface.
- **Good fit for on-prem:** no need to ship a large client app or manage complicated browser-side state.
- **MudBlazor works well here:** it supports straightforward business UI without requiring a front-end-heavy architecture.
- **Security is simpler:** data access and trace logic remain server-side.
- **Useful for internal latency patterns:** staff are likely on the same network or VPN, so interactive server-side UI is acceptable.

### Why not WASM for MVP

Blazor WebAssembly would add complexity without solving a pressing business problem. It would require more client-side state management, more API surface, and more deployment complexity, while the main challenge of this project is domain correctness, not disconnected client behavior.

Revisit WASM only if later phases require:

- low-latency external access over poor networks
- offline/mobile workflows
- a public/external user experience

## Recommended architecture style

**Use a modular monolith.**

Do not split this system into services for MVP. The main risk in this project is incorrect or hard-to-maintain domain logic, not service scaling.

A modular monolith keeps:

- deployment simple
- debugging simple
- transactions simple
- domain logic easier to follow
- ownership realistic for a small internal team

## High-level structure

Recommended logical layers:

1. **UI**
   - Blazor Server pages/components
   - MudBlazor-based forms, tables, filters, dialogs, drawers
2. **Application layer**
   - use-case orchestration
   - validation coordination
   - trace request handling
   - import workflow coordination
3. **Domain layer**
   - location hierarchy rules
   - graph expansion logic
   - movement overlap logic
   - disease profile interpretation
4. **Infrastructure layer**
   - EF Core / SQL Server persistence
   - import staging and batch execution
   - authentication/authorization integration
   - reporting/export helpers as needed

## Recommended deployment shape

For MVP:

- one Blazor Server application
- one SQL Server database
- one shared configuration source
- optional import runner inside the application or as an admin-only companion job/tool

This is enough unless usage grows far beyond current expectations.

## Major components

## 1. Facility and location management

Responsible for:

- facilities
- room-like spaces
- kennels
- parent/child structure
- active/inactive state
- map display metadata

This component replaces the Word-based layout source.

## 2. Relationship graph management

Responsible for explicit links between locations, including:

- kennel adjacency
- room connections
- airflow links
- transport-path links
- other trace-relevant relationships

This is the core graph data used by trace logic.

## 3. Animal movement tracking

Responsible for:

- animal identity needed by this app
- current placement
- historical movement intervals
- overlap-ready history for tracing

This component should use interval-based records (`StartUtc`, `EndUtc`) rather than only point-in-time move logs.

## 4. Disease tracing

Responsible for:

- selecting disease profile
- selecting source animal or source stay
- selecting time window
- expanding impact through adjacency/topology rules
- finding overlapping animals
- returning explainable results

This should be implemented as a cohesive domain/application service, not scattered across UI code.

## 5. Import and migration

Responsible for:

- canonical spreadsheet ingestion
- validation
- staging
- safe promotion to production tables
- re-runnable imports
- migration from manual documents

This is a first-class concern, not an afterthought.

## 6. Authorization

For MVP, keep authorization simple:

- `ReadOnly`
- `Admin`

If your environment supports integrated Windows authentication, that is a strong fit for on-prem use. Otherwise ASP.NET Core Identity is fine. The domain plan only depends on role claims, not on a specific identity provider.

## Recommended storage model

### Core recommendation

Use a **single `Locations` table with a `LocationType`** rather than separate SQL tables for every physical space type.

This is one place where **simplicity is better than purity**.

Why this is better for MVP:

- one movement table can reference one location key
- one graph table can link any two locations
- future space types do not require schema redesign
- the internal team has fewer tables and joins to reason about

In business language, **Room** and **Kennel** still matter. In storage, they can be represented as location types.

## Data flow for contact tracing

Recommended request flow:

```text
User selects:
  Facility (optional if derived)
  Disease profile
  Source animal or source stay
  Trace window
  Optional location scope

Application flow:
  1. Resolve source movement interval(s)
  2. Resolve source location(s)
  3. Build initial impacted set
  4. Expand impacted set using disease profile rules:
       - same location
       - parent room
       - adjacent kennels
       - linked rooms/spaces via allowed link types
  5. If a location scope was selected, filter impacted results to that location and its containment descendants
  6. Find movement overlaps between impacted locations and trace window
  7. Exclude the seed/source animal from impacted-animal output
  8. Group results by:
       - impacted locations
       - impacted animals
       - inclusion reason
  9. Return results with explainability metadata
```

### Important tracing rule

Do **not** infer operational adjacency from the display grid alone.

Grid coordinates help users see layout, but trace logic should use explicit stored links. If a relationship matters clinically, it should be present in graph data.

## Disease rule design

For MVP, do **not** build a generic rules engine.

Instead use a small, explicit model:

- `Disease`
- `DiseaseTraceProfile`
- optional allowed link types
- simple numeric depth settings

That gives flexibility without inventing a framework.

A practical profile can answer questions such as:

- should same room be included?
- should adjacent kennels be included?
- how many adjacency hops are allowed?
- should airflow links be included?
- should transport-path links be included?
- what is the default lookback window?

If later diseases truly need different algorithms, introduce strategies then. Not before.

## Persistence approach

### Primary recommendation

Use **EF Core** for mainstream persistence and migrations.

Why:

- readable for the internal team
- productive for standard CRUD/admin screens
- integrates well with Blazor Server
- adequate for expected MVP scale

### Practical exception

Use targeted SQL or specialized query code where it clearly improves:

- batch imports
- validation queries
- heavy reporting
- trace queries that become awkward or inefficient in LINQ

Avoid a generic repository abstraction over EF Core. It usually adds ceremony without value in systems like this.

## Scalability and simplicity tradeoffs

## What to keep simple now

- modular monolith instead of services
- server-side Blazor
- form/table-based editors instead of drag-and-drop
- admin/script-driven import instead of import wizard
- on-demand trace calculation instead of background jobs
- explicit rule profiles instead of a rule engine

## What to design for later

- additional disease profiles
- richer link types
- more facilities
- better visual editing
- trace result export and saved trace runs
- integration with other shelter systems

## Expected MVP scale

This system is unlikely to hit internet-scale problems. The critical concerns are:

- data correctness
- trace explainability
- ease of maintenance
- staff usability

For MVP, correctness and clarity matter more than advanced scaling patterns.

## Operational notes

If you later run more than one web node for Blazor Server:

- share data-protection keys
- ensure SignalR/session behavior is configured correctly
- test real user workflows under that topology

Do not optimize for that before there is a real need.

## Summary recommendation

Use:

- **Blazor Server**
- **MudBlazor**
- **SQL Server**
- **EF Core**
- **modular monolith**
- **single unified locations model**
- **explicit graph links**
- **interval-based movement history**
- **simple configurable disease profiles**

That is the best balance of maintainability, correctness, and delivery speed for this project.
