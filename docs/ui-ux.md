# UI / UX

The UI should help staff do three things well:

1. understand current facility layout
2. review animal movement history
3. run and understand contact traces

Because the current process is manual, the MVP should emphasize **clarity, familiarity, and low-friction admin forms** over sophisticated visual editing.

## General UX principles

- Use shelter language, not technical language.
- Prefer **one obvious workflow** over many options.
- Make the current source of truth visible.
- Show incomplete data honestly.
- Always explain trace results with a reason.
- Keep editing workflows form-based in MVP.
- Use color sparingly and consistently; do not rely on color alone.

## Main screens

## 1. Kennel map view

### Purpose

Allow staff to browse a facility and room, see kennel placement, and inspect a selected kennel or room.

### Layout

Recommended layout:

```text
+---------------------------------------------------------------+
| Facility selector | Room selector | Filters | Legend          |
+-------------------+-------------------------------------------+
| Left info/filter  | Main map/grid area        | Detail panel  |
| panel (optional)  |                           | selected item  |
|                   | [kennel tiles/cards]      | occupant/link  |
|                   | [unplaced kennel section] | details        |
+---------------------------------------------------------------+
```

### What the user sees

- facility dropdown
- room dropdown
- optional filter chips:
  - occupied / vacant
  - active / inactive
  - show unplaced kennels
- grid of kennel cards
- selected kennel details in a side panel or drawer
- fallback “Unplaced Kennels” section for missing grid data

### Interaction patterns

- Click a kennel tile to view details.
- Click a room header or breadcrumb to move back up context.
- Hover or tap for quick tooltip details if useful.
- Keep interactions simple; no drag-and-drop in MVP.

### MudBlazor suggestions

- `MudSelect` for facility and room selection
- `MudChipSet` / `MudChip` for filters and legend
- `MudPaper` or `MudCard` for kennel tiles
- `MudGrid` for layout
- `MudDrawer` or right-side panel for selected details
- `MudTooltip` for small hints
- `MudAlert` for incomplete data warnings

### MVP simplicity guidance

- Render a practical grid, not a precise floor plan.
- Show missing map placement clearly instead of hiding kennels.
- Do not block map viewing when adjacency data is incomplete.

## 2. Animal detail view

### Purpose

Show current placement, movement history, and trace entry point for one animal.

### Layout

Recommended layout:

```text
+---------------------------------------------------------------+
| Animal summary card                                           |
| ID | name | status | current location | current facility      |
+---------------------------------------------------------------+
| Current placement panel | Quick actions                       |
|                         | Run trace | Record move             |
+---------------------------------------------------------------+
| Movement history timeline/table                               |
+---------------------------------------------------------------+
```

### What the user sees

- animal summary
- current placement
- current room/facility
- movement history in reverse chronological order
- quick action to start a trace from the animal
- optional notes or flags if data is incomplete

### Interaction patterns

- “Run trace” should carry the selected animal into the trace page.
- Movement history should be readable as a time sequence.
- If there is an open stay, display it clearly as current.

### MudBlazor suggestions

- `MudCard` for summary
- `MudText` and `MudChip` for status indicators
- `MudButton` for actions
- `MudTable` for movement history
- `MudTimeline` if it remains readable; otherwise prefer table first
- `MudExpansionPanels` for secondary details

### MVP simplicity guidance

- Prefer a table over an elaborate visual timeline if it is easier to maintain.
- Do not overload the screen with every possible animal field; show only what is useful for location and tracing.

## 3. Contact trace view

### Purpose

Let staff run a trace and understand the result without technical interpretation.

### Layout

Recommended layout:

```text
+---------------------------------------------------------------+
| Trace inputs                                                  |
| Disease | Source animal/stay | Date range | Optional scope    |
+---------------------------------------------------------------+
| Trace summary / warnings                                      |
+---------------------------------------------------------------+
| Tabs: Impacted Locations | Impacted Animals | Why Included    |
+---------------------------------------------------------------+
```

### What the user sees

Inputs:

- disease profile selector
- animal search or selected source stay
- date/time range
- optional facility or location filter

Location scope behavior for MVP:

- if a location scope is selected, the result tabs should show only the selected persisted location and its containment descendants
- the source animal itself should not appear in impacted-animal results

Outputs:

- trace summary
- impacted locations table
- impacted animals table
- “why included” explanations
- warnings when data is incomplete

### Interaction patterns

- Keep trace inputs on one page unless they become too crowded.
- Support sensible defaults from the disease profile.
- Group results by reason and location where it improves readability.
- Allow clicking from impacted animal or location into its detail view.

### MudBlazor suggestions

- `MudAutocomplete` for animal search
- `MudSelect` for disease and scope
- `MudDateRangePicker` or paired date/time pickers
- `MudButton` to run trace
- `MudTabs` for result views
- `MudTable` for impacted locations/animals
- `MudChip` for reason labels
- `MudAlert` for caveats or incomplete graph data

### MVP simplicity guidance

- The most important UX feature is **explainability**, not visualization polish.
- Results should say things like:
  - “Same kennel during overlapping time”
  - “Adjacent right to source kennel”
  - “Same room as source kennel”
  - “Airflow-linked room”
- Avoid an overcomplicated wizard unless user testing proves it necessary.

## 4. Facility topology editor

### Purpose

Allow admins to maintain room-like spaces, kennels, and trace-relevant links.

### Layout

Recommended layout:

```text
+---------------------------------------------------------------+
| Facility selector | Actions                                   |
+---------------------------------------------------------------+
| Location tree/list | Selected item form | Links table         |
| rooms/areas        | code/name/type     | from/to/type/notes  |
| kennels under room | grid fields        | add/remove link      |
+---------------------------------------------------------------+
```

### What the user sees

- facility selector
- tree/list of spaces
- selected item form
- editable fields for code, name, type, active status
- kennel fields for row, column, stack level
- table of outgoing/incoming links
- add/edit/remove link actions

### Interaction patterns

- Select a location from the left pane.
- Edit details on the right.
- Manage links in a table or dialog.
- For kennel placement, type row/column/stack values directly.
- For adjacency, choose target kennel and relationship type from a form.

### MudBlazor suggestions

- `MudTreeView` or grouped `MudList` for facility structure
- `MudForm`
- `MudTextField`
- `MudNumericField`
- `MudSelect`
- `MudCheckBox`
- `MudTable` for links
- `MudDialog` for add/edit link flows
- `MudButtonGroup` for save/cancel actions

### MVP simplicity guidance

- Do not build a visual graph editor first.
- Do not require drag-and-drop to maintain the layout.
- Use structured forms and tables because they are easier to ship, test, and support.

## Additional UI recommendations

## Navigation

Top-level navigation for MVP should stay small:

- Facilities / Kennel Map
- Animals
- Contact Trace
- Admin
  - Facilities & Locations
  - Imports
  - Disease Profiles

## Empty states and warnings

Because migration data will be imperfect, good empty states matter:

- “No kennels placed on the map yet.”
- “This room has no adjacency links defined.”
- “This trace used partial graph data; results reflect only stored relationships.”

## Readability

- Prefer labels over icons alone.
- Prefer tables with clear column names over dense cards when reviewing data.
- Use chips for status/reason categories, not paragraphs of explanation everywhere.

## What to avoid in MVP

- freeform canvas editing
- hidden auto-save behavior
- multiple competing screens for the same task
- large modal-heavy workflows for simple edits
- inferring adjacency behind the scenes without showing it to admins

## Summary

For MVP, the right UI is:

- map-like where it helps understanding
- form-based where it helps maintenance
- table-based where it helps traceability
- explicit about incomplete data
- optimized for clarity rather than visual sophistication
