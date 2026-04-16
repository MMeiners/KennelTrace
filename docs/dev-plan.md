# Development Plan

This plan is sequenced to deliver value quickly, reduce rework, and keep the system grounded in real shelter operations.

## Guiding principle

Do the work in the order that locks the **data foundation first**, then gives staff **readable visibility**, then adds **editing and tracing**.

That is the shortest path to replacing the manual process without painting the project into a corner.

## Step-by-step build order

## Step 1: Lock the vocabulary and natural keys

### Deliverables

- confirmed location terminology
- final `LocationType` set for MVP
- final `LinkType` set for MVP
- natural key decisions for facilities, locations, and animals
- explicit decision that grid is visual aid, graph is trace authority

### Why first

If these drift later, imports, schema, UI labels, and trace logic all get more expensive to change.

### Codex should generate first

- domain enums/constants
- core entity definitions
- shared validation primitives

## Step 2: Implement the database foundation

### Deliverables

- initial SQL schema / EF Core migrations for:
  - facilities
  - locations
  - location links
  - animals
  - movement events
  - diseases / trace profiles
  - import batches/issues
- seed/reference data for location/link types if needed

### Why now

This stabilizes the core model before UI work expands.

### Codex should generate next

- EF Core models and mappings
- migrations
- basic domain invariants
- initial test scaffolding

## Step 3: Build the import validation pipeline

### Deliverables

- canonical workbook specification
- import reader
- validation-only mode
- row-level error/warning reporting
- commit mode for successful batches
- import batch logging

### Why now

This is the fastest route to replacing the manual process with real facility data.

### Codex should generate next

- import DTOs
- file/sheet validators
- natural-key upsert logic
- integration tests using sample workbooks

## Step 4: Load one pilot facility

### Deliverables

- first real facility converted from manual documents
- validation issues resolved
- confirmed codes and location/link conventions

### Why now

This is the earliest possible reality check. It reveals modeling mistakes before too much UI is built.

### Testing checkpoint with real users

Have shelter staff review:

- room names
- kennel names/codes
- adjacency correctness
- topology links that matter operationally

## Step 5-7: Build the read-only facility and kennel map

### Deliverables

- facility selector
- room selector
- simple kennel map/grid
- selected kennel detail panel
- unplaced kennel fallback display

### Why now

This gives staff immediate operational value and validates whether the imported layout is understandable.

### Codex should generate next

- read-only pages/components
- basic query services
- simple room map rendering

### Testing checkpoint with real users

Ask staff whether they can stop using the Word document for lookup in at least one facility.

## Step 8: Build admin maintenance for locations and links

### Deliverables

- admin screens for facilities/locations
- link management UI
- grid placement editing
- active/inactive handling

### Why now

Once staff can view the data, admins need a way to maintain it without round-tripping every fix through scripts.

### Codex should generate next

- admin forms
- validation messages
- save/update workflows
- role-based route protection

### Testing checkpoint with real users

Have an admin correct known layout issues and verify that the changes are easier than editing the old documents.

## Step 9: Build animal records and movement history

### Deliverables

- animal lookup/detail page
- current placement display
- movement history table
- record movement workflow
- overlap/open-stay validation

### Why now

Tracing is only as good as movement history. This step creates the time-based data needed for trace logic.

### Codex should generate next

- animal pages
- movement entry logic
- movement validation tests
- integration tests around overlaps and current stay handling

### Testing checkpoint with real users

Walk through a few real movement scenarios from intake to transfer to isolation.

## Step 10: Build disease profiles and trace service

### Deliverables

- disease profile model
- trace input model
- graph expansion logic
- overlap matching logic
- explainable results with reasons

### Why now

By this point, the app has layout, graph links, and movement history. The trace logic can now be built against real data.

### Codex should generate next

- `ContactTraceService`
- trace result models
- scenario-based unit tests
- integration tests using pilot facility data

### Testing checkpoint with real users

Run trace exercises based on real historical or hypothetical disease scenarios and verify that results match staff expectations.

## Step 11: Build trace UI

### Deliverables

- trace input page
- impacted locations tab
- impacted animals tab
- reason/explainability tab
- warning messages for partial data

### Why now

The UI should sit on top of already-tested trace logic rather than defining it.

### Codex should generate next

- trace page/components
- result tables
- navigation links from animal and map screens into trace flow

## Step 12: Refine with real usage before Phase 2 features

### Deliverables

- usability improvements based on staff feedback
- targeted performance fixes if needed
- clarified backlog for phase 2
- decision on whether richer visual editing is actually needed

### Why now

The real users will tell you which pain points are worth solving next. That is more valuable than guessing.

## How to avoid rework

## 1. Lock natural keys early

Do not build imports or admin editing around surrogate IDs only. Natural keys are necessary for re-runs and migration.

## 2. Treat explicit links as authoritative

Do not build trace logic around inferred grid adjacency. You will regret this when real layouts are irregular.

## 3. Keep disease rules simple at first

Do not build a rules engine before you have several real disease workflows that require one.

## 4. Build read-only visibility before fancy editing

A map users can trust is more valuable than a sophisticated editor they do not need yet.

## 5. Use one pilot facility as the proving ground

A real facility will expose gaps faster than whiteboard design.

## 6. Write scenario tests from real shelter patterns

This prevents regressions in the parts that matter most.

## Suggested Codex generation order

If you want Codex to implement this with minimal ambiguity, the best generation order is:

1. domain entities and enums
2. SQL/EF Core schema and mappings
3. import DTOs, validators, and batch workflow
4. read-only facility/kennel map
5. admin location/link maintenance
6. animal and movement workflows
7. disease profiles and contact trace service
8. trace UI
9. targeted refinements only after real-user feedback

## Real-user checkpoints

Use these checkpoints deliberately.

### Checkpoint A: after pilot import

Goal: confirm the imported structure matches reality.

### Checkpoint B: after read-only map

Goal: confirm staff can actually use the app instead of the Word document.

### Checkpoint C: after admin editing

Goal: confirm layout maintenance is practical for the people who own the data.

### Checkpoint D: after movement history

Goal: confirm the app records enough operational history for tracing.

### Checkpoint E: after first trace workflow

Goal: confirm the trace output is trusted and understandable.

## What not to do first

Do not start with:

- drag-and-drop mapping
- polished floor plans
- generic abstractions
- advanced reporting
- integration work
- saved trace runs
- full import wizard

Those all risk slowing down the point of the project: replacing the manual process quickly and correctly.

## Recommended definition of “first valuable release”

A first valuable release should include:

- one or more facilities loaded
- room/kennel hierarchy
- explicit adjacency/topology links
- read-only map
- admin editing for corrections
- movement history
- one working disease trace flow

That is enough for a real pilot.

## Summary

The build order should be:

- model the truth
- import the truth
- let staff see the truth
- let admins maintain the truth
- use the truth for movement and tracing

That sequence gives the fastest path to practical value with the least avoidable rework.
