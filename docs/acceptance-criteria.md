# Acceptance Criteria

This document defines what “done” means for each MVP feature, with attention to edge cases and data integrity.

## 1. Multi-facility setup and location registry

### Done when

- An admin can create and edit facilities.
- An admin can create room-like locations and kennel locations within a facility.
- A read-only user can browse facilities and locations but cannot modify them.
- Each location has at least:
  - facility
  - type
  - code
  - display name
  - active/inactive state
- Kennels can be assigned to a valid parent room-like space.

### Edge cases

- Different facilities may reuse the same room or kennel code, as long as codes are unique within a facility.
- A location may be inactive but retained for historical traceability.
- A room-like location may exist before all its child kennels are known.
- A kennel may temporarily exist without grid coordinates.

### Data integrity expectations

- Facility code is unique.
- Location code is unique within a facility.
- Parent and child locations belong to the same facility.
- Parent chains cannot form cycles.
- Kennels cannot be physically deleted if movement history exists; they should be deactivated instead.

## 2. Kennel map data with simple grid placement

### Done when

- A kennel may be assigned optional `GridRow`, `GridColumn`, and `StackLevel`.
- The selected room can be shown as a simple grid-based map.
- Kennels with valid coordinates render in grid order.
- Kennels missing coordinates still appear in an “Unplaced” list or equivalent fallback area.
- Grid placement can be edited by an admin without drag-and-drop.

### Edge cases

- Two kennels may share a row and column only if stack level distinguishes them.
- Some kennels may intentionally have no grid placement but still participate in tracing via explicit links.
- A room may contain both placed and unplaced kennels.

### Data integrity expectations

- Negative grid values are rejected.
- Duplicate `(Room, GridRow, GridColumn, StackLevel)` positions are rejected.
- Grid data is never treated as the sole source of adjacency truth.

## 3. Explicit adjacency and facility topology management

### Done when

- An admin can create and remove trace-relevant links between locations.
- The system supports at least these link types in MVP:
  - `AdjacentLeft`
  - `AdjacentRight`
  - `AdjacentAbove`
  - `AdjacentBelow`
  - `AdjacentOther`
  - `Connected`
  - `Airflow`
  - `TransportPath`
- Links are queryable for trace logic.
- Directional inverse links are handled consistently by the application or import process.

### Edge cases

- Irregular layouts can be represented with `AdjacentOther`.
- A room may have airflow or transport-path links even if it has no kennel grid.
- A kennel can be traceable even if not visually placed on the map.
- A link may be imported from uncertain historical data and kept with notes/source reference.

### Data integrity expectations

- Self-links are rejected.
- Cross-facility links are rejected in MVP.
- Duplicate links of the same type and direction are rejected.
- Directional pairs such as left/right and above/below must remain logically consistent.
- Adjacency-style links should only connect kennel locations; topology-style links should connect room-like locations unless an admin deliberately overrides that rule.

## 4. Read-only facility/kennel map view

### Done when

- A read-only user can:
  - choose a facility
  - choose a room-like space
  - view kennel map layout
  - select a kennel or room and see details
- The UI clearly distinguishes:
  - kennel identity
  - parent room
  - current occupant if known
  - relevant link information
- Users can navigate the current layout without needing edit permissions.

### Edge cases

- The room has no kennels.
- The room has kennels but some are unplaced.
- The selected kennel is inactive or vacant.
- The room data is incomplete but still viewable.

### Data integrity expectations

- The view reflects the current persisted data only.
- The view does not silently infer missing adjacency links from grid placement.
- Read-only users cannot access editing actions through UI or route manipulation.

## 5. Animal records and movement history

### Done when

- An admin can create or edit the minimal animal identity data needed by the app.
- An admin can record a movement/stay for an animal in a location.
- The app shows current placement and prior movement history in time order.
- Open/current stays are supported with `EndUtc = null`.
- Movement history is traceable by date/time overlap.

### Edge cases

- An animal moves multiple times within one day.
- An open stay is later closed when the animal moves again.
- Historical data import includes uncertain times.
- An animal moves between facilities over time.

### Data integrity expectations

- `StartUtc` is required.
- `EndUtc`, when present, must be greater than `StartUtc`.
- Manual entry prevents overlapping stays for the same animal.
- At most one open/current stay exists per animal.
- Movement records remain historically visible even if locations later become inactive.

## 6. Disease profiles and contact tracing

### Done when

- A user can run a trace by selecting:
  - disease profile
  - source animal or source stay
  - time window
  - optional location scope
- The result includes:
  - impacted locations
  - impacted animals
  - inclusion reason(s)
- The trace honors disease profile rules such as:
  - same location
  - same room
  - adjacent kennels
  - selected topology link types
  - configured depth/lookback values
- The result is understandable without reading logs or code.

### Edge cases

- The source animal has multiple stays in the selected window.
- The source stay is still open.
- A location has partial graph data; trace still returns what can be proven from stored data.
- The disease profile excludes some link types.
- Optional location scope narrows the result.
- Optional location scope narrows impacted results to the selected persisted location and its containment descendants; it does not discard source stays before graph expansion.
- The seed/source animal is not returned in impacted-animal results, even when its own later stays would otherwise overlap an impacted location.

### Data integrity expectations

- Trace logic uses explicit stored links, not guessed adjacency from coordinates.
- Time overlap comparisons are deterministic and test-covered.
- Every impacted result includes at least one reason code or explanation.
- Trace results are reproducible from persisted movement and link data.

## 7. Admin/script-driven import and migration pipeline

### Done when

- Canonical spreadsheet inputs are defined for:
  - room-like spaces
  - kennels
  - adjacency/topology links
- An admin or technical operator can run a validation step before commit.
- Validation reports identify errors by file, sheet, and row.
- A successful import can insert or update data safely.
- Imports can be re-run without creating uncontrolled duplicates.

### Edge cases

- The file references a room that does not exist yet.
- The file includes duplicate location codes.
- The file includes conflicting left/right relationships.
- The file includes incomplete grid placement.
- The file includes spaces that are intentionally inactive.

### Data integrity expectations

- Imports use natural keys, not only surrogate IDs.
- Imports fail as a batch if required references are missing.
- Invalid rows are not silently skipped during a commit import.
- Link replacement is deterministic for the selected facility/scope.
- Import batch metadata is stored for audit/troubleshooting.

## 8. Basic authorization and audit metadata

### Done when

- The application supports at least `ReadOnly` and `Admin`.
- Read-only users cannot create, edit, or delete layout, movement, or trace profile data.
- Admin actions are attributable to a user account where the platform makes that practical.

### Edge cases

- A user’s role changes after data was created.
- A read-only user attempts a direct navigation to an admin route.
- A failed save must not partially commit protected changes.

### Data integrity expectations

- Authorization is enforced server-side, not only in the UI.
- Audit metadata does not block core workflow if optional fields are unavailable.
- Restricted actions either complete fully or fail cleanly.
