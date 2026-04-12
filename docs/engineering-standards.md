# Engineering Standards

These standards are intentionally pragmatic.

The goal is to produce a codebase that a small internal team can understand, extend, and debug without ceremony.

## Core principles

Prioritize:

- readability
- maintainability
- explicit business rules
- low coupling where it matters
- testability of important logic
- straightforward naming
- small number of moving parts

Do **not** chase architecture for its own sake.

## Project structure

## Recommended starting point

Start with **no more than four main projects**:

```text
src/
  KennelTrace.Web
  KennelTrace.Domain
  KennelTrace.Infrastructure
tests/
  KennelTrace.Tests
```

### Purpose of each

- `KennelTrace.Web`
  - Blazor Server UI
  - page/component composition
  - application/use-case orchestration
  - authorization policies

- `KennelTrace.Domain`
  - entities
  - value objects if truly useful
  - domain services
  - business rules for layout, movement, and tracing

- `KennelTrace.Infrastructure`
  - EF Core DbContext
  - migrations
  - SQL/data access
  - import execution
  - external integrations if added later

- `KennelTrace.Tests`
  - unit tests
  - integration tests
  - scenario tests

## Avoid premature project splitting

Do **not** add separate assemblies for every conceptual layer on day one.

For example, avoid creating all of these unless real complexity appears:

- `Application`
- `Contracts`
- `SharedKernel`
- `Common`
- `Abstractions`
- `Repositories`
- `Specifications`

That kind of split often increases navigation cost without improving maintainability.

## Organize by feature inside projects

Within `Web` and `Domain`, prefer feature-oriented folders:

```text
Features/
  Facilities/
  Locations/
  Animals/
  Tracing/
  Imports/
```

This is usually easier to follow than one giant folder for each technical artifact type.

## Naming conventions

## General rules

- Use names from the shelter domain.
- Prefer nouns for domain entities.
- Prefer strong verbs for use cases.
- Avoid vague “utility” naming.

### Good examples

- `Facility`
- `Location`
- `LocationLink`
- `MovementEvent`
- `DiseaseTraceProfile`
- `RunContactTrace`
- `RecordMovement`
- `ValidateImportBatch`

### Avoid

- `DataManager`
- `Helper`
- `Util`
- `Processor` when a more specific name exists
- `BaseService`
- `CommonFunctions`

## Method and class design

## Favor cohesive methods

A method should do one coherent thing, but that does **not** mean every method should be tiny.

Use extraction when it improves one of these:

- names a real business concept
- removes duplication
- separates a tricky rule for testing
- makes the main flow easier to read

Do **not** extract methods just to create one-line wrappers.

### Preferred

```text
RunContactTrace(...)
  - resolve source stays
  - expand impacted locations
  - load overlapping animal stays
  - build result reasons
```

### Not preferred

```text
DoStep1()
DoStep2()
DoStep3()
```

when those names do not add any domain meaning.

## Domain modeling guidance

## Prefer straightforward domain models

A simple model that the team understands is better than a “cleaner” model that is hard to navigate.

Recommended examples for this project:

- one `Location` concept with `LocationType`
- one `LocationLink` concept for trace-relevant relationships
- one interval-based `MovementEvent`

Avoid over-modeling unless requirements demand it.

## Keep business rules explicit

Important rules should be visible in code, not scattered across UI, database triggers, and helper classes.

Examples that deserve explicit rule handling:

- inverse adjacency consistency
- movement overlap prevention
- trace expansion rules
- import validation
- facility boundary rules

## Data access standards

## Primary approach

Use **EF Core** as the default persistence approach.

This gives:

- readable mappings
- consistent migrations
- fast iteration for admin screens
- fewer custom data-access abstractions

## Practical exception

Use direct SQL or focused query objects when it is clearly better for:

- batch imports
- heavy validation queries
- trace/report queries that become awkward in LINQ

## Avoid a generic repository layer

A generic repository over EF Core usually creates indirection without adding real value.

Prefer:

- `DbContext` for routine persistence
- focused query/service classes for special reads
- domain/application services for business rules

## Testing strategy

## What should be tested most

Put the most effort into test coverage for business rules that could create bad operational outcomes:

- location hierarchy rules
- adjacency/topology link validation
- movement overlap logic
- disease trace graph expansion
- trace reason generation
- import validation and reconciliation rules

## Recommended test mix

### Unit tests

Use for:

- interval overlap logic
- graph traversal/expansion
- disease profile behavior
- inverse link handling
- validation rules

### Integration tests

Use for:

- EF Core mappings
- SQL query behavior
- import batch execution
- trace queries against realistic data scenarios

### UI tests

Use sparingly and only for high-value workflows, such as:

- trace form wiring
- map screen key interactions
- authorization on critical admin screens

Do not try to achieve exhaustive UI automation for MVP.

## Where TDD is most valuable

TDD is most useful for the parts where correctness matters and inputs/outputs are well defined:

- contact trace service
- movement interval rules
- import validators
- link normalization/inverse creation
- regression scenarios based on real shelter layouts

It is much less valuable to force TDD on every MudBlazor form or trivial CRUD page.

## Pragmatic SOLID guidance

Use SOLID as a set of design instincts, not a ritual.

## Single Responsibility Principle

Interpret this as **cohesion**, not “smallest possible method/class.”

A class is fine if it owns one coherent responsibility even when it has multiple methods.

## Open/Closed Principle

Do not build plugin systems preemptively.

For this project, a disease rule strategy is worth introducing only when there are multiple genuinely different trace algorithms that are becoming awkward in one place.

## Interface Segregation / Dependency Inversion

Introduce interfaces when at least one of these is true:

- there is an external dependency you want to isolate
- there are multiple implementations with real value
- the abstraction reduces coupling in tests or architecture
- the dependency is unstable or infrastructure-specific

### Good candidates for interfaces

- clock/time provider
- file import reader
- external animal system integration
- notification/integration boundaries

### Poor candidates for interfaces

- every application service
- every EF-backed data access class
- simple classes with only one foreseeable implementation

## Avoid over-engineering

Red flags for this project:

- many tiny pass-through classes
- interfaces with one implementation and no clear boundary purpose
- repository layers that only wrap EF Core
- abstract factories for simple object construction
- elaborate event buses inside a single app
- building a generic rules engine before real disease variation exists
- splitting the solution into many packages too early

## Preferred balance examples

### Good balance

- One `ContactTraceService` with cohesive helper methods and strong tests.
- One unified `Location` model rather than one table/class per location subtype.
- One graph link model with clear `LinkType` values.
- One import pipeline with validate/commit modes.

### Too much ceremony

- `IContactTraceService`
- `ITraceGraphBuilder`
- `ITraceExpansionPolicy`
- `ITraceNodeRepository`
- `ITraceReasonFactory`

all introduced before there is real variation or test pressure.

## Code style guidance

- Prefer explicit conditionals over clever compact expressions when business rules are involved.
- Prefer guard clauses for invalid input.
- Keep public APIs small and intention-revealing.
- Use comments to explain **why**, not to narrate obvious code.
- Keep DTOs, entities, and view models clearly named for their purpose.
- Avoid giant “god classes,” but also avoid exploding one concept into ten files.

## Domain scenarios as living tests

Create a small set of representative scenario fixtures based on real shelter patterns:

- simple linear kennel room
- stacked kennels
- irregular room with missing grid placement
- airflow-linked isolation path
- animal moved across multiple rooms during trace window

These scenario tests will catch regressions better than large amounts of shallow CRUD testing.

## Change management guidance

When adding new abstractions, ask:

1. What concrete pain does this solve right now?
2. Will the next developer understand it quickly?
3. Does it reduce risk or just move code around?
4. Is this simpler than leaving the logic explicit?

If the answer is weak, do not add the abstraction yet.

## Summary

The engineering standard for this project is:

- explicit business rules
- modest structure
- strong tests where correctness matters
- minimal ceremony
- feature-oriented organization
- interfaces only at meaningful boundaries
- readable, cohesive code over architectural theater
