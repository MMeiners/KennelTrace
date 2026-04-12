# Domain Model

## Modeling principles

This system needs to model the shelter in a way that is both operationally useful and simple enough for a small internal team to maintain.

The recommended model is guided by five principles:

1. **One physical place = one location record**
   - A room, kennel, hallway, yard, medical area, or isolation area is represented as a `Location`.
   - Different kinds of places are distinguished by `LocationType`.

2. **Containment and relationship are different concepts**
   - Containment answers **“where does this place belong?”**
   - Relationship answers **“what places matter together for tracing?”**
   - The model must support both.

3. **Grid is for display, graph is for logic**
   - Grid coordinates help staff see the layout.
   - Explicit graph links drive adjacency and tracing behavior.
   - The system should not quietly infer truth from row/column coordinates.

4. **Movement is time-span based, not point-event based**
   - Tracing depends on overlap between animal stays.
   - The system stores time intervals for location occupancy.

5. **MVP should optimize clarity over theoretical purity**
   - Prefer a single unified location model.
   - Prefer explicit business rules over generic abstractions.
   - Design for extension later without introducing a rules engine now.

---

## Canonical domain terms

The project should use a few domain terms consistently.

| Domain term | Meaning | Typical storage concept |
|---|---|---|
| Facility | A physical shelter site or campus | `Facilities` |
| Location | Any physical place inside a facility | `Locations` |
| Room | A location that can organize or contain kennels/spaces | `Locations` with `LocationType = Room` |
| Kennel | A location used for direct animal housing | `Locations` with `LocationType = Kennel` |
| Link | A trace-relevant relationship between locations | `LocationLinks` |
| Stay / Placement | A time interval where an animal occupied a location | `MovementEvents` |
| Disease Trace Profile | Configurable tracing rules for a disease/category | `DiseaseTraceProfiles` |

### Important language choice

The database table may still be called `MovementEvents`, but in domain discussions it should be thought of as a **stay interval** or **placement interval**, not a point-in-time event.

That framing makes the tracing rules much clearer.

---

## Core entities

## Facility

A physical shelter site or campus.

Examples:

- Main shelter
- Isolation annex
- Secondary adoption site

A facility is the top-level boundary for layout data.

### Facility rules

- Every location belongs to exactly one facility.
- Direct `LocationLink` relationships stay within one facility in MVP.
- Cross-facility tracing happens through animal movement history, not through topology links.

---

## Location

A physical place within a facility.

Use one unified `Location` concept with a `LocationType`.

Typical location types:

- `Room`
- `Hallway`
- `Medical`
- `Isolation`
- `Intake`
- `Yard`
- `Kennel`
- `Other`

This keeps the model simple enough for MVP while still supporting different kinds of spaces.

### Why one unified location model

Using one `Location` model avoids separate movement models and link models for every type of place.

That simplifies:

- imports
- tracing queries
- movement history
- admin editing
- future extension to new space types

### Location identity rules

A `Location` represents a **physical place**, not just a label on a screen.

That means:

- renaming a kennel does **not** create a new location
- moving a kennel to a physically different place usually **does** require a new location
- inactive/retired locations should generally be preserved for historical traceability
- locations with movement history should not be hard-deleted in normal workflows

---

## Animal

A dog being housed or tracked by the shelter.

For MVP, the animal model only needs enough data to:

- identify the animal
- display current placement
- review movement history
- run traces

Do not overdesign this if another shelter system eventually becomes the richer animal system of record.

---

## MovementEvent

A time-bounded record that an animal occupied a location.

Recommended shape:

- `AnimalId`
- `LocationId`
- `StartUtc`
- `EndUtc` (nullable for current/open stay)
- optional movement reason / notes / recorded-by metadata

Despite the table name, this is conceptually a **stay interval**.

---

## Disease

A disease or tracing category such as parvo.

Keep this simple in MVP:

- code
- display name
- active/inactive
- notes

---

## DiseaseTraceProfile

A configurable trace behavior profile attached to a disease.

Typical settings:

- default lookback hours
- include same location
- include same room
- include adjacent kennels
- adjacency depth
- include specific topology link types
- topology depth

This should stay configurable but simple.

Do **not** turn MVP into a generic rules engine.

---

## Supporting concepts

### ImportBatch

Represents one import execution for facility layout data.

Useful for:

- traceability
- dry-run/commit flow
- safe re-runs
- troubleshooting migration issues

### ImportIssue

Represents row-level validation feedback during imports.

This matters because migration from Word/manual records is one of the core business problems the system is solving.

---

## Location capability matrix

The unified `Location` model works best when the allowed behavior of each type is explicit.

| LocationType | Can contain child locations | Can directly host animal stays | Usually shown on kennel grid | Can participate in topology links |
|---|---:|---:|---:|---:|
| Kennel | No | Yes | Yes | No in MVP |
| Room | Yes | Optional | No | Yes |
| Hallway | No in MVP | Usually no | No | Yes |
| Medical | Yes | Yes | No | Yes |
| Isolation | Yes | Yes | No | Yes |
| Intake | Yes | Yes | No | Yes |
| Yard | No in MVP | Yes | No | Yes |
| Other | Case by case | Optional | No | Yes |

### Notes on this matrix

- `Kennel` is the main housing unit shown on the map/grid.
- `Room` and similar space-like locations organize the facility and participate in topology/tracing.
- `Medical`, `Isolation`, `Intake`, and `Yard` may directly host animals because animals may be physically present there during treatment, intake, quarantine, or exercise.
- `Kennel` does **not** participate in topology links in MVP; kennel-to-kennel relationships are handled through adjacency links.
- `Hallway` and `Yard` may gain child locations later if a real need appears, but MVP should not assume that.

---

## Containment rules

Containment models **where a place belongs in the facility structure**.

### Typical hierarchy

```text
Facility
├── Room A
│   ├── Kennel A1
│   ├── Kennel A2
│   └── Kennel A3
├── Hallway 1
├── Treatment Room
└── Isolation Room
```

### Recommended containment rules

- every location belongs to one facility
- parent and child must belong to the same facility
- location parent chains must never be cyclic
- kennels cannot contain child locations
- kennels should usually have a room-like parent
- hallways should normally be topological spaces, not containment parents
- yards should normally be topological spaces, not containment parents
- same-room relationships are derived from containment, not stored as links

### Allowed parent/child combinations for MVP

| Parent type | Allowed child types |
|---|---|
| Facility (implicit root) | Room, Hallway, Medical, Isolation, Intake, Yard, Other |
| Room | Kennel, Other room-like space if truly needed |
| Medical | Kennel, Other room-like space if truly needed |
| Isolation | Kennel, Other room-like space if truly needed |
| Intake | Kennel, Other room-like space if truly needed |
| Hallway | None in MVP |
| Yard | None in MVP |
| Kennel | None |

### Why keep containment narrow

Containment is easy to overgeneralize.

A narrow rule set helps:

- imports validate cleanly
- the admin UI stay understandable
- trace logic stay predictable
- historical data remain coherent

If a real facility later needs more nesting, the model can expand from a known baseline.

---

## LocationLink

A `LocationLink` represents a trace-relevant relationship between two locations.

This is the graph edge model.

It is used for two related but different purposes:

1. **Kennel adjacency**
2. **Facility topology**

### Why use one link concept

Kennel adjacency and broader facility relationships are both forms of:

**“location A is related to location B in a way that may matter for tracing.”**

Using one `LocationLink` concept is simpler than maintaining separate edge systems unless later requirements prove they need materially different behavior.

---

## Link catalog

The link catalog should be explicit enough that imports, validation, tracing, and tests all use the same rules.

### Link families

- **Adjacency links** describe close-proximity kennel relationships.
- **Topology links** describe broader space-to-space relationships.

### Recommended MVP catalog

| LinkType | Family | Typical endpoints | Semantic direction | Inverse type | Trace usage |
|---|---|---|---|---|---|
| `AdjacentLeft` | Adjacency | Kennel -> Kennel | Directional | `AdjacentRight` | Immediate kennel adjacency |
| `AdjacentRight` | Adjacency | Kennel -> Kennel | Directional | `AdjacentLeft` | Immediate kennel adjacency |
| `AdjacentAbove` | Adjacency | Kennel -> Kennel | Directional | `AdjacentBelow` | Immediate kennel adjacency |
| `AdjacentBelow` | Adjacency | Kennel -> Kennel | Directional | `AdjacentAbove` | Immediate kennel adjacency |
| `AdjacentOther` | Adjacency | Kennel <-> Kennel | Symmetric | `AdjacentOther` | Irregular or clinically relevant proximity |
| `Connected` | Topology | Space <-> Space | Symmetric in MVP | `Connected` | Basic movement/contact connectivity |
| `Airflow` | Topology | Space <-> Space | Symmetric in MVP | `Airflow` | Shared airflow exposure |
| `TransportPath` | Topology | Space <-> Space | Symmetric in MVP | `TransportPath` | Staff/animal movement route relevance |

### Endpoint conventions

In this catalog:

- **Kennel** means a `Location` with `LocationType = Kennel`
- **Space** means a non-kennel location such as `Room`, `Hallway`, `Medical`, `Isolation`, `Intake`, `Yard`, or `Other`

### Storage recommendation for all links

Even when a link is semantically symmetric, store links as **explicit directed rows**.

That means:

- `Connected(Room A, Hallway 1)` should usually be stored as both:
  - `Room A -> Hallway 1`
  - `Hallway 1 -> Room A`
- `AdjacentOther(Kennel A, Kennel B)` should usually be stored as both directions as well

### Why store reciprocal rows explicitly

This is slightly redundant, but it makes the application simpler:

- traversal logic is uniform
- import validation is easier to reason about
- trace queries do not need special “reverse edge” handling
- tests become more straightforward

### Recommended link validation rules

- no self-links
- no cross-facility links in MVP
- link endpoints must exist and be active unless an explicit admin correction workflow allows otherwise
- adjacency links must be kennel-to-kennel only
- topology links should be space-to-space only in MVP
- inverse directional pairs should be kept consistent
- duplicate directed links should be rejected
- links are authoritative for tracing; grid coordinates are not

### Practical recommendation for MVP

Treat `Airflow` and `TransportPath` as **symmetric** unless you already have real, trustworthy data that requires directional behavior.

That keeps the model simple while leaving room to introduce directional variants later if needed.

---

## Movement and occupancy semantics

This section should be treated as one of the main sources of truth for implementation and testing.

### Core concept

A `MovementEvent` is really a **stay interval**:

```text
Animal X stayed in Location Y from StartUtc until EndUtc
```

### Time semantics

Use **half-open interval semantics**:

```text
[StartUtc, EndUtc)
```

Meaning:

- the stay includes `StartUtc`
- the stay excludes `EndUtc`
- an open stay with `EndUtc = null` is treated as continuing indefinitely until closed

### Why half-open intervals are important

They avoid false overlap during handoff.

Example:

```text
Stay A: Kennel 1, 08:00 -> 12:00
Stay B: Kennel 2, 12:00 -> 15:00
```

These should **not** be treated as overlapping stays.

### Movement rules

- one animal may have **at most one open/current stay**
- one animal may not have overlapping stays in different locations
- moving an animal is represented by closing one stay and opening another
- current placement is derived from the open stay, not stored separately as an independent truth source
- cross-facility movement is just consecutive stays in locations belonging to different facilities

### Occupancy rules for MVP

Do **not** hard-code the assumption that one location can only host one animal at a time.

Instead:

- allow multiple animals to have overlapping stays in the same location
- treat occupancy/capacity rules as a separate concern from trace history
- if the shelter later needs one-animal-only or capacity-based validation, add it explicitly rather than baking it into the history model prematurely

### Why this is the better MVP choice

It keeps the history model accurate for real-world shelter behavior such as:

- temporary co-housing
- animals sharing yards or intake spaces
- exceptional operational situations

If you later need stricter constraints, add:

- `Capacity`
- location-level occupancy policies
- warning or blocking validation during placement changes

### Suggested overlap rules for tracing

When tracing:

- same-location exposure is based on overlapping stay intervals in the same location
- same-room exposure is based on overlapping stays in child locations that share the same room-like parent, or direct occupancy of the same room-like location
- adjacency exposure is based on overlap plus matching adjacency links between occupied kennels
- topology exposure is based on overlap plus traversal through selected topology links according to the disease profile

### Data correction posture

Movement history should be **append-only in spirit** even if admins are allowed to fix mistakes.

That means:

- prefer corrective edits only for bad data cleanup
- avoid workflows that casually rewrite past placement history
- retain metadata about who changed what when practical

---

## Trace conceptual model

The entity model above exists to support trace operations.

A useful way to think about tracing is through four conceptual outputs.

## TraceRequest

Defines the input to a trace run.

Typical fields:

- seed animal
- disease / trace profile
- facility
- time window
- optional explicit seed location
- optional included location filter

## ImpactedLocation

A location included by the trace.

Typical fields:

- location
- inclusion reason
- path depth
- source link type, if applicable

## ExposedAnimal

An animal returned by the trace.

Typical fields:

- animal
- overlapping stay(s)
- impacted location
- inclusion reason

## TraceReason

A human-meaningful reason why a result was included.

Recommended MVP reasons:

- `SameLocation`
- `SameRoom`
- `Adjacent`
- `AirflowLinked`
- `TransportPathLinked`
- `ConnectedSpace`

### Important rule

Trace logic should return not just **who** was included, but **why** they were included.

This is important for staff trust, troubleshooting, and testing.

---

## Grid vs graph

This is one of the most important distinctions in the system.

## Grid

The grid is a **display and editing aid**.

It helps answer:

- where should a kennel appear on the map?
- what row/column is it shown in?
- how should a stacked or irregular layout be rendered?

Typical fields:

- `GridRow`
- `GridColumn`
- `StackLevel`
- `DisplayOrder`

The grid is useful for staff and for simple map rendering.

## Graph

The graph is the **operational relationship model**.

It answers:

- which kennels are actually adjacent?
- which spaces are connected by hallway or route?
- which spaces share airflow relevance?
- which relationships should matter during tracing?

### Recommended rule

- use the **grid for UI**
- use the **graph for logic**

Do not let the system silently infer adjacency from coordinates and treat that as authoritative truth.

---

## How adjacency and topology differ

## Adjacency

Adjacency is usually a close-proximity relationship between kennels.

Examples:

- left/right
- above/below
- irregular “clinically close” neighbor relationships

Adjacency is local and kennel-focused.

## Topology

Topology is about broader facility relationships between spaces.

Examples:

- rooms connected by a hallway
- airflow between areas
- transport paths used by staff
- shared routes to treatment or isolation

Topology is broader and space-focused.

### Why the distinction matters

A disease trace may need to consider:

- only direct kennel adjacency for one disease
- same room plus airflow-linked spaces for another
- transport path relationships for a third

That means the model must preserve **what kind of relationship** exists, not just that two places are somehow “near.”

---

## Worked examples

## Example 1: simple kennel adjacency

```text
Room A
├── Kennel A1
├── Kennel A2
└── Kennel A3

Links:
A1 --AdjacentRight--> A2
A2 --AdjacentLeft-->  A1
A2 --AdjacentRight--> A3
A3 --AdjacentLeft-->  A2
```

Notes:

- the room relationship comes from containment
- adjacency comes from explicit links
- the grid may render the same order, but the links are still the source of truth

## Example 2: irregular layout

```text
Room B
├── Kennel B1
├── Kennel B2
└── Kennel B3

Grid display may show B3 below B1,
but only explicit links determine adjacency.
```

Notes:

- `AdjacentOther` is useful for clinically relevant proximity that is not well represented by row/column position
- this is one reason the graph must be authoritative

## Example 3: topology-based tracing

```text
Treatment Room <-> Hallway 1 <-> Isolation Room
Treatment Room <-> Airflow <-> Prep Room
```

Notes:

- a disease profile may traverse `Connected` links but ignore `TransportPath`
- another profile may include `Airflow`
- link type matters, not just graph reachability

## Example 4: movement handoff

```text
Animal 101
- Kennel A1: 2026-04-01 08:00 -> 2026-04-02 12:00
- Kennel A2: 2026-04-02 12:00 -> 2026-04-03 09:30
- Isolation: 2026-04-03 09:30 -> null
```

Notes:

- the first and second stays do not overlap because of half-open interval semantics
- tracing against adjacent kennels depends on overlap during each stay window

---

## Recommended domain invariants

- facility code is unique
- location code is unique within a facility
- parent and child locations must belong to the same facility
- location parent chains must not be cyclic
- kennel locations must not have child locations
- no location link may reference the same location as both endpoints
- no adjacency link may connect non-kennel locations
- no topology link should connect kennel locations in MVP
- one animal may not have overlapping stays
- one animal may have at most one open stay
- trace logic must always return inclusion reason(s)

---

## Recommended future extension points

These are worth designing for now, but not fully implementing in MVP:

- saved trace runs and audit history
- effective-dated `LocationLinks` if facility topology changes often over time
- richer disease profiles
- weighted or severity-based links
- capacity and occupancy policies
- external animal-system integration
- visual topology editor

### Important note on historical topology

For MVP, it is reasonable to treat layout/topology as slowly changing master data.

If the shelter later needs fully retrospective “what did the topology look like on a past date?” tracing, the next extension should be **effective-dated links**, not a completely different model.

---

## Summary

The cleanest practical model for MVP is:

- one **Facility**
- one unified **Location** model with types
- one explicit **LocationLink** graph with a clear link catalog
- one interval-based **MovementEvent** history with explicit overlap semantics
- one simple **DiseaseTraceProfile**

That is enough to replace the manual Word-driven process with a structured, queryable source of truth while keeping the design understandable for a small internal team.
