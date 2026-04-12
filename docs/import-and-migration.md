# Import and Migration

Import/migration is a first-class requirement in this project.

The shelter already has layout knowledge in Word documents and ad hoc notes. MVP should provide a **technical but realistic onboarding path** without requiring a polished end-user wizard.

## Recommendation

- **MVP:** admin/script-driven import
- **Phase 2:** optional UI-driven upload/validation workflow

This is the right tradeoff for speed, maintainability, and adoption.

## Canonical import format

Recommended format: **one workbook per facility layout version** with standardized sheets.

Suggested file naming:

```text
{FacilityCode}_Layout_{yyyyMMdd}.xlsx
```

### Required sheets for MVP

1. `Rooms`
2. `Kennels`
3. `LocationLinks`

### Optional sheet

4. `Facilities` (only needed if facilities are not pre-created)

## Sheet 1: Rooms

This sheet represents room-like locations, including hallways and specialty spaces.

### Purpose

Create or update room/container spaces that kennels belong to or that matter to topology/tracing.

### Required columns

| Column | Type | Required | Notes |
|---|---|---:|---|
| FacilityCode | string(50) | Yes | Must match existing or imported facility |
| RoomCode | string(50) | Yes | Natural key within facility |
| RoomName | string(100) | Yes | Display name |
| RoomType | enum/string(30) | Yes | `Room`, `Hallway`, `Medical`, `Isolation`, `Intake`, `Yard`, `Other` |
| ParentLocationCode | string(50) | No | Optional parent room/area within same facility |
| IsActive | boolean | No | Default `TRUE` |
| DisplayOrder | int | No | Optional UI sort hint |
| SourceReference | string(200) | No | Word doc name/page, note source, etc. |
| Notes | string(500) | No | Freeform migration notes |

### Validation rules

- `FacilityCode` must exist or be part of the same batch.
- `RoomCode` must be unique within the facility.
- `RoomType` must be one of the allowed values.
- `ParentLocationCode`, if provided, must exist in the same facility.
- Parent relationships cannot create cycles.

## Sheet 2: Kennels

### Purpose

Create or update kennel locations and their simple map placement metadata.

### Required columns

| Column | Type | Required | Notes |
|---|---|---:|---|
| FacilityCode | string(50) | Yes | Must match existing or imported facility |
| RoomCode | string(50) | Yes | Parent room/location code |
| KennelCode | string(50) | Yes | Natural key within facility |
| KennelName | string(100) | No | Defaults to code if blank |
| GridRow | int | No | Optional for MVP map rendering |
| GridColumn | int | No | Optional for MVP map rendering |
| StackLevel | int | No | Default `0` if blank |
| DisplayOrder | int | No | Optional UI ordering |
| IsActive | boolean | No | Default `TRUE` |
| SourceReference | string(200) | No | Manual source note |
| Notes | string(500) | No | Freeform migration notes |

### Validation rules

- `RoomCode` must reference an existing room-like location in the same facility.
- `KennelCode` must be unique within the facility.
- `GridRow`, `GridColumn`, `StackLevel` must be non-negative if provided.
- Duplicate `(RoomCode, GridRow, GridColumn, StackLevel)` positions are not allowed.
- Missing grid coordinates are allowed.
- Grid coordinates do not imply adjacency.

## Sheet 3: LocationLinks

This single sheet handles both kennel adjacency and broader topology links.

### Purpose

Load trace-relevant explicit relationships between locations.

### Required columns

| Column | Type | Required | Notes |
|---|---|---:|---|
| FacilityCode | string(50) | Yes | Must match both endpoints |
| FromLocationCode | string(50) | Yes | Source endpoint |
| ToLocationCode | string(50) | Yes | Target endpoint |
| LinkType | enum/string(30) | Yes | See allowed values below |
| CreateInverse | boolean | No | Default `TRUE` where meaningful |
| SourceReference | string(200) | No | Word doc/page or note source |
| Notes | string(500) | No | Freeform migration notes |

### Allowed MVP link types

Adjacency-style:

- `AdjacentLeft`
- `AdjacentRight`
- `AdjacentAbove`
- `AdjacentBelow`
- `AdjacentOther`

Topology-style:

- `Connected`
- `Airflow`
- `TransportPath`

### Validation rules

- Both endpoints must exist in the same facility.
- `FromLocationCode` cannot equal `ToLocationCode`.
- Duplicate `(FromLocationCode, ToLocationCode, LinkType)` rows are not allowed.
- Conflicting inverse relationships must be flagged.
- Cross-facility links are not allowed in MVP.
- Adjacency-style links should connect kennels.
- Topology-style links should connect room-like spaces.

## Optional sheet: Facilities

Only needed if facilities are not pre-seeded.

| Column | Type | Required | Notes |
|---|---|---:|---|
| FacilityCode | string(50) | Yes | Natural key |
| FacilityName | string(200) | Yes | Display name |
| TimeZoneId | string(100) | Yes | Windows or agreed application timezone key |
| IsActive | boolean | No | Default `TRUE` |
| Notes | string(500) | No | Optional |

## Why one link sheet is better than separate adjacency and topology sheets

It reduces complexity during migration:

- one relationship model
- one validation path
- one set of natural keys
- easier to explain to technical staff preparing files

The distinction still exists in the `LinkType` values.

## Migration strategy from Word/manual records

## Step 1: Inventory current documents

For each facility:

- identify all current Word docs, diagrams, spreadsheets, and notes
- note which document is currently treated as “most trusted”
- capture unresolved ambiguities explicitly

## Step 2: Establish stable codes

Before importing, assign stable codes for:

- facility
- room-like spaces
- kennels

Codes matter because imports must be re-runnable and traceable.

## Step 3: Convert manual data into canonical spreadsheets

Translate the current manual process into:

- `Rooms`
- `Kennels`
- `LocationLinks`

Do not try to clean every ambiguity silently. Carry uncertain items forward with `SourceReference` and notes.

## Step 4: Run validation in staging

Import should first load to staging/validation logic and report:

- missing references
- duplicate codes
- conflicting links
- invalid types
- grid collisions
- cycle risks

## Step 5: Resolve errors with staff review

Technical operators should review the validation report with shelter staff who understand the real-world layout.

This is where the application helps: it makes uncertainty visible instead of burying it in documents.

## Step 6: Commit a clean batch

Only after validation passes should the system update production tables.

## Step 7: Cut over the source of truth

After a facility is loaded and verified:

- archive the Word/manual layout documents
- mark them as historical reference only
- direct staff to the application as the maintained source of truth

## Import execution model for MVP

## Recommended modes

### 1. Validate only

- parse files
- check references
- produce errors and warnings
- write no operational data

### 2. Commit

- only allowed after validation succeeds
- writes production data in a controlled transaction/scope
- records batch metadata and issues

## Error handling approach

Use **errors** and **warnings**.

### Errors

Import must fail commit when errors exist.

Examples:

- missing facility
- missing room reference
- duplicate location code
- invalid link type
- self-link
- cycle in parent hierarchy
- conflicting inverse relationship

### Warnings

Import may still succeed, but warnings must be visible.

Examples:

- kennel has no grid placement
- room has no links yet
- link imported with note indicating uncertainty
- room exists with no active kennels

## Safe re-run strategy

This matters a lot. Migration data will change as staff refine it.

### Use natural keys

Match records by:

- `FacilityCode`
- `(FacilityCode, RoomCode/KennelCode)`
- `(FacilityCode, FromLocationCode, ToLocationCode, LinkType)`

### Recommended update behavior

#### Rooms and kennels

Use **upsert by natural key**:

- insert if new
- update if existing
- deactivate only when explicitly instructed, not by omission unless a “replace” mode is chosen

#### Links

Recommended option for MVP:

- validate the full incoming link set for a facility
- replace the targeted link set atomically after validation passes

This is usually safer than trying to merge complex directional graph data row-by-row over time.

## Import auditability

Each import batch should record:

- source file name
- file hash if practical
- operator
- time started/completed
- validate-only vs commit
- status
- row-level issues

This makes migration and troubleshooting much easier.

## Suggested operating model for MVP

### Who performs imports

A technical admin, DBA, or trusted internal operator.

### Where imports happen

Either:

- a restricted admin screen, or
- a companion import tool/script executed by technical staff

### What not to build yet

Do not build in MVP:

- end-user spreadsheet mapping UI
- custom per-file column mapping
- complex merge conflict resolution UI
- freeform data-repair tools in the app

Those are expensive and not needed to replace the current process.

## Practical migration tips

- Start with one pilot facility.
- Expect at least one cleanup cycle after the first validation report.
- Capture unresolved layout ambiguity explicitly instead of guessing.
- Import the smallest accurate graph first; add refinement later.
- Prefer correct explicit links over “pretty” map placement.

## Summary recommendation

For MVP, the import/migration approach should be:

- canonical spreadsheet format
- admin/script-driven execution
- validate first
- batch-level audit trail
- safe upsert/replacement behavior
- one graph link model for both adjacency and topology

That is the fastest realistic path away from Word-driven layout management.
