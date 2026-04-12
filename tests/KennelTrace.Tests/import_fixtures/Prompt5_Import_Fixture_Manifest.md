# Prompt 5 import fixture pack

These fixtures were aligned to the canonical workbook shape in `docs/import-and-migration.md`.

## Files

### 1. `PHX_MAIN_Layout_20260412.xlsx`
Expected result: **validation succeeds with no errors**.

Covers:
- canonical sheet names
- optional `Facilities` sheet present
- natural keys across `Facilities`, `Rooms`, `Kennels`, and `LocationLinks`
- adjacency and topology links
- `CreateInverse = TRUE`

### 2. `PHX_WARN_Layout_20260412_warnings.xlsx`
Expected result: **validation succeeds with warnings only**.

Suggested warnings to assert:
- `Kennels` row 2: kennel has no grid placement
- `Rooms` row 5: room exists with no active kennels
- `LocationLinks` row 3: imported note indicates uncertainty

### 3. `PHX_BAD_Layout_20260412_invalid_rows.xlsx`
Expected result: **validation fails with row-level errors**.

Suggested errors to assert:

#### Rooms
- row 5: duplicate `RoomCode` within facility
- row 6: invalid `RoomType`
- row 7: missing `ParentLocationCode`
- rows 8-9: parent cycle

#### Kennels
- row 4: `RoomCode` does not exist in facility
- row 5: negative `GridRow`
- row 6: duplicate `(RoomCode, GridRow, GridColumn, StackLevel)`
- row 7: duplicate `KennelCode` within facility

#### LocationLinks
- row 3: self-link
- row 4: adjacency-style link on room-like endpoints
- row 5: invalid `LinkType`
- row 6: duplicate directed link
- row 7: conflicting inverse relationship
- row 9: topology-style link on kennel endpoints
- row 10: cross-facility / wrong-facility endpoint reference

### 4. `PHX_MISS_Layout_20260412_missing_locationlinks.xlsx`
Expected result: **sheet validation fails** because `LocationLinks` is missing.

### 5. `PHX_HDR_Layout_20260412_bad_headers.xlsx`
Expected result: **sheet/header validation fails**.

Suggested header assertions:
- `Rooms.A1` is `Facility` instead of `FacilityCode`
- `Rooms.D1` is `RoomTyp` instead of `RoomType`
- `Kennels.F1` is `GridCol` instead of `GridColumn`
- `LocationLinks.D1` is `LinkKind` instead of `LinkType`

## Notes

- These fixtures intentionally use **natural keys**.
- The valid workbook keeps the canonical sheet names exactly as documented.
- The invalid fixtures are designed for **validate-only mode first**, with readable row-level reporting.
- Boolean fields are stored as Excel booleans (`TRUE` / `FALSE`).
- `TimeZoneId` uses `US Mountain Standard Time` for Phoenix-friendly sample data.
