# AGENTS.md

## Purpose

KennelTrace is an internal kennel management and contact tracing application for shelter staff.

The first goal is not flashy automation. The first goal is to replace Word-based layout records and ad hoc notes with a structured, queryable source of truth for:

- facilities
- locations and room/kennel hierarchy
- trace-relevant location links
- animal movement history
- disease tracing workflows

## Current repository state

This repository currently starts as a planning and design package. Unless code files clearly exist, do **not** assume a scaffolded solution, migrations, CI pipeline, or validated run commands already exist.

Rules for working in this repo:

- Do not pretend code, tests, or commands exist when they do not.
- Do not claim build/test success unless you actually ran the commands in the current repository.
- If you scaffold code, also update `README.md` and this file with the real build/test commands.
- If you change canonical behavior, keep docs and code synchronized in the same task.

## Read first

For most work, read documents in this order:

1. `README.md`
2. `docs/acceptance-criteria.md`
3. `docs/domain-model.md`
4. `docs/architecture.md`
5. `docs/dev-plan.md`
6. `docs/engineering-standards.md`
7. Supporting docs:
   - `docs/features.md`
   - `docs/data-model.sql.md`
   - `docs/import-and-migration.md`
   - `docs/ui-ux.md`
   - `docs/vision.md`

When documents overlap or conflict, use this source-of-truth order:

1. acceptance criteria
2. domain model
3. architecture
4. data model
5. development plan
6. supporting docs

When code and tests exist, keep them aligned with the acceptance criteria and domain model.

## Build and test commands

There is **no authoritative application build/test command yet** until the solution is scaffolded.

Once implementation exists, document and verify the real commands here and in `README.md`, for example:

- restore
- build
- test
- database migration/update workflow

Until then, do not invent a working command set.

## Non-negotiable domain rules

These rules should stay consistent across code, schema, import workflows, and UI.

- The graph is authoritative. The grid is a display aid.
- One physical place maps to one `Location`.
- Containment and trace relationships are separate concerns.
- Trace logic must use explicit stored links, never guessed adjacency from row/column coordinates.
- Movement history is interval-based with `StartUtc` and nullable `EndUtc`.
- Treat stays as half-open intervals: `[StartUtc, EndUtc)`.
- Prevent overlapping stays for the same animal.
- Allow at most one open/current stay per animal.
- Natural keys matter for import, reconciliation, and safe re-runs.
- Preserve history. If movement history exists, prefer deactivation over deletion.
- Every trace result must include at least one explicit inclusion reason.

Canonical MVP values:

- `LocationType`:
  - `Room`
  - `Hallway`
  - `Medical`
  - `Isolation`
  - `Intake`
  - `Yard`
  - `Kennel`
  - `Other`
- adjacency-style link types:
  - `AdjacentLeft`
  - `AdjacentRight`
  - `AdjacentAbove`
  - `AdjacentBelow`
  - `AdjacentOther`
- topology-style link types:
  - `Connected`
  - `Airflow`
  - `TransportPath`

MVP boundary rules:

- `FacilityCode` is unique.
- `LocationCode` is unique within a facility.
- Parent and child locations must belong to the same facility.
- Parent chains must not be cyclic.
- Kennels must have a valid parent room-like location.
- Self-links are invalid.
- Cross-facility links are invalid in MVP.
- Adjacency-style links should connect kennels.
- Topology-style links should connect room-like locations unless an intentional admin override is being built explicitly.

## Architecture expectations

Prefer this stack and shape unless requirements change materially:

- ASP.NET Core Blazor Server
- MudBlazor
- EF Core
- SQL Server
- modular monolith
- internal / on-prem deployment posture

Recommended project structure once code exists:

```text
docs/
src/
  KennelTrace.Web
  KennelTrace.Domain
  KennelTrace.Infrastructure
tests/
  KennelTrace.Tests
```

Within `Web` and `Domain`, prefer feature-oriented folders:

```text
Features/
  Facilities/
  Locations/
  Animals/
  Tracing/
  Imports/
```

Do **not** introduce these for MVP unless there is a concrete, current need:

- microservices
- separate distributed event bus
- generic rules engine
- generic repository layer over EF Core
- many small pass-through abstractions
- polished drag-and-drop mapping before the data foundation is trusted

## Implementation order

Default build order:

1. lock vocabulary, enums, and natural keys
2. implement SQL/EF Core schema and mappings
3. build import DTOs, validators, and batch workflow
4. load one pilot facility from real data
5. build read-only facility and kennel map
6. add admin maintenance for locations and links
7. add animal and movement workflows
8. build disease profiles and trace service
9. build trace UI
10. refine only after real shelter feedback

This sequence matters. Do not start with polished floor plans, advanced reporting, external integrations, or saved trace runs.

## Coding conventions

- Use shelter-domain names consistently.
- Prefer nouns for entities and strong verbs for use cases.
- Avoid vague names like `Utils`, `Helpers`, or `Manager` unless the class has a clear bounded purpose.
- Keep DTOs, entities, persistence models, and view models clearly distinct.
- Prefer explicit conditionals and guard clauses when business rules are involved.
- Extract methods when doing so names a real business concept, improves testability, or removes duplication.
- Do not extract one-line wrappers just to make methods shorter.
- Comments should explain **why**, not narrate obvious code.
- Keep public APIs intention-revealing and small.
- Prefer cohesive, readable code over architectural ceremony.

## Data access guidance

- Use EF Core as the default persistence approach.
- Use direct SQL or focused query objects only when clearly better for batch imports, heavy validation, or complex trace/report queries.
- Keep important rules in application/domain services or explicit validators, not hidden in UI handlers or scattered helpers.
- Do not add a generic repository layer that only wraps `DbContext`.

## Testing expectations

Put the most effort into tests for:

- location hierarchy rules
- adjacency/topology validation
- inverse link handling
- movement overlap logic
- trace graph expansion
- trace reason generation
- import validation and reconciliation

Prefer a mix of:

- focused unit tests for rule-heavy services and validators
- integration tests for persistence and import workflows
- scenario tests based on real shelter patterns

Useful scenario fixtures:

- simple linear kennel room
- stacked kennels
- irregular room with unplaced kennels
- airflow-linked isolation path
- animal moving across multiple rooms during the trace window

Do not force exhaustive TDD on trivial CRUD or simple MudBlazor forms, but use TDD where correctness is critical.

## Imports and migration

Treat import and migration as a first-class feature, not a cleanup task.

Rules:

- Use natural keys, not only surrogate IDs.
- Validate first, then commit.
- Report errors with file, sheet, and row context.
- Batch commit should fail cleanly when required references are missing.
- Invalid rows must not be silently skipped during a commit run.
- Link replacement/reconciliation must be deterministic for the selected facility/scope.
- Store import batch metadata for audit and troubleshooting.

## Authorization and audit

MVP roles are at least:

- `ReadOnly`
- `Admin`

Requirements:

- enforce authorization server-side, not only in the UI
- do not leave admin routes or protected handlers writable through direct navigation
- protected writes should complete fully or fail cleanly
- keep audit metadata where practical, but do not make optional audit fields block core workflow

## Change management

When changing canonical behavior, update the relevant docs in the same task. Typical cases:

- domain term or invariant change -> `docs/domain-model.md` and possibly `docs/acceptance-criteria.md`
- schema or key change -> `docs/data-model.sql.md`, import docs, and affected tests
- import shape change -> `docs/import-and-migration.md`
- user-visible workflow change -> `docs/ui-ux.md` and/or `README.md`
- implementation sequencing change -> `docs/dev-plan.md`

If you add a new enum value, link type, or domain rule, do not update only the code.

## Planning rule

Before large or risky work, make a short plan first. This especially applies to tasks that:

- touch schema plus import logic
- change trace semantics
- span more than one feature area
- add new abstractions or dependencies

Prefer small, reviewable increments over broad speculative rewrites.

## Review guidelines

Flag or reject changes that:

- infer adjacency from grid coordinates
- blur containment and link semantics
- break natural keys or facility boundaries
- allow overlapping stays for the same animal
- return trace hits without reasons
- delete historical locations or movements that should be retained
- enforce authorization only in the UI
- add generic repository, event-bus, or rules-engine ceremony without clear need
- introduce domain terminology that conflicts with `docs/domain-model.md`

## Done when

A task is not done until all of the following are true:

- the change matches `docs/acceptance-criteria.md`
- the domain vocabulary remains consistent
- relevant tests were added or updated for rule-heavy behavior
- documentation was updated when canonical behavior changed
- the final summary clearly states what changed, what was verified, and any remaining assumptions or limits
