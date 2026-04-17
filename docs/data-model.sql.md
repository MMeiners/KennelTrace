# Draft SQL Server Data Model

This schema draft is aligned to the revised domain model and is intentionally opinionated in a few places so implementation work does not drift.

The main alignment points are:

- one unified `Locations` table with `LocationType`
- containment modeled by `ParentLocationId`
- adjacency and topology both modeled by `LocationLinks`
- `MovementEvents` treated as **stay intervals**, not point events
- half-open interval semantics for overlap logic: `[StartUtc, EndUtc)`
- **same-room** exposure derived from containment, not stored as a separate link
- no hard-coded one-animal-per-location rule in MVP

This is still a pragmatic MVP schema. It aims to be understandable to a small internal team and explicit enough that Codex does not have to guess at key invariants.

---

## Schema principles

### 1. One physical place = one location row

A room, kennel, hallway, isolation area, yard, or intake space is represented in the same `Locations` table.

That keeps:

- movement history simple
- imports simple
- graph traversal simple
- UI and tracing aligned to one spatial model

### 2. Containment and graph relationships are separate

Containment answers:

- what room or parent space does this location belong to?

Graph links answer:

- what other spaces or kennels are trace-relevant neighbors?

Do not merge those ideas into one table or one rule set.

### 3. Grid is for UI, graph is for logic

`GridRow`, `GridColumn`, `StackLevel`, and `DisplayOrder` help render the kennel map.

They are **not** the source of truth for adjacency.

### 4. Movement is interval-based

A row in `MovementEvents` means:

> animal X stayed in location Y from `StartUtc` until `EndUtc`

This is critical for deterministic trace overlap behavior.

### 5. Enforce what SQL can enforce cleanly

Some invariants are worth enforcing directly in the database:

- same-facility parent/child relationships
- kennels must have a parent
- same-facility links
- no self-links
- one open stay per animal
- duplicate active directed links not allowed

Other invariants are better enforced in application/service logic:

- no overlapping stays for the same animal
- valid link endpoint type combinations
- inverse link pair creation/removal
- cycle detection beyond simple self-parent prevention

---

## Catalog values used by the schema

These are intentionally fixed in MVP. If the catalog changes, change it through a normal migration.

### LocationType values

- `Room`
- `Hallway`
- `Medical`
- `Isolation`
- `Intake`
- `Yard`
- `Kennel`
- `Other`

### LinkType values

| LinkType | Family | Typical endpoints | Inverse | Notes |
|---|---|---|---|---|
| `AdjacentLeft` | Adjacency | Kennel -> Kennel | `AdjacentRight` | directional |
| `AdjacentRight` | Adjacency | Kennel -> Kennel | `AdjacentLeft` | directional |
| `AdjacentAbove` | Adjacency | Kennel -> Kennel | `AdjacentBelow` | directional |
| `AdjacentBelow` | Adjacency | Kennel -> Kennel | `AdjacentAbove` | directional |
| `AdjacentOther` | Adjacency | Kennel <-> Kennel | `AdjacentOther` | symmetric meaning, stored as two directed rows |
| `Connected` | Topology | Space <-> Space | `Connected` | symmetric meaning, stored as two directed rows |
| `Airflow` | Topology | Space <-> Space | `Airflow` | symmetric meaning in MVP |
| `TransportPath` | Topology | Space <-> Space | `TransportPath` | symmetric meaning in MVP |

### SourceType values

Used on tables that need lightweight provenance.

- `Manual`
- `Import`
- `Derived`

---

## Core tables

## 1. Facilities

```sql
CREATE TABLE Facilities (
    FacilityId            INT IDENTITY PRIMARY KEY,
    FacilityCode          NVARCHAR(50)  NOT NULL,
    Name                  NVARCHAR(200) NOT NULL,
    TimeZoneId            NVARCHAR(100) NOT NULL,
    IsActive              BIT           NOT NULL DEFAULT 1,
    Notes                 NVARCHAR(500) NULL,
    CreatedUtc            DATETIME2     NOT NULL,
    ModifiedUtc           DATETIME2     NOT NULL,

    CONSTRAINT UQ_Facilities_FacilityCode
        UNIQUE (FacilityCode)
);
```

### Notes

- `FacilityCode` is the natural key used by import/reconciliation.
- `TimeZoneId` is stored even if the current deployment mostly uses one time zone.
- Facilities are the top-level layout boundary.

---

## 2. Locations

```sql
CREATE TABLE Locations (
    LocationId            INT IDENTITY PRIMARY KEY,
    FacilityId            INT           NOT NULL,
    ParentLocationId      INT           NULL,
    LocationType          NVARCHAR(30)  NOT NULL,
    LocationCode          NVARCHAR(50)  NOT NULL,
    Name                  NVARCHAR(100) NOT NULL,
    GridRow               INT           NULL,
    GridColumn            INT           NULL,
    StackLevel            INT           NOT NULL DEFAULT 0,
    DisplayOrder          INT           NULL,
    IsActive              BIT           NOT NULL DEFAULT 1,
    Notes                 NVARCHAR(500) NULL,
    CreatedUtc            DATETIME2     NOT NULL,
    ModifiedUtc           DATETIME2     NOT NULL,

    CONSTRAINT UQ_Locations_LocationId_Facility
        UNIQUE (LocationId, FacilityId),

    CONSTRAINT UQ_Locations_Facility_LocationCode
        UNIQUE (FacilityId, LocationCode),

    CONSTRAINT FK_Locations_Facilities
        FOREIGN KEY (FacilityId) REFERENCES Facilities(FacilityId),

    CONSTRAINT FK_Locations_ParentSameFacility
        FOREIGN KEY (ParentLocationId, FacilityId)
        REFERENCES Locations (LocationId, FacilityId),

    CONSTRAINT CK_Locations_NotOwnParent
        CHECK (ParentLocationId IS NULL OR ParentLocationId <> LocationId),

    CONSTRAINT CK_Locations_KennelHasParent
        CHECK (LocationType <> 'Kennel' OR ParentLocationId IS NOT NULL),

    CONSTRAINT CK_Locations_LocationType
        CHECK (LocationType IN (
            'Room', 'Hallway', 'Medical', 'Isolation',
            'Intake', 'Yard', 'Kennel', 'Other'
        )),

    CONSTRAINT CK_Locations_GridRow
        CHECK (GridRow IS NULL OR GridRow >= 0),

    CONSTRAINT CK_Locations_GridColumn
        CHECK (GridColumn IS NULL OR GridColumn >= 0),

    CONSTRAINT CK_Locations_StackLevel
        CHECK (StackLevel >= 0),

    CONSTRAINT CK_Locations_GridCoordinatesTogether
        CHECK (
            (GridRow IS NULL AND GridColumn IS NULL)
            OR
            (GridRow IS NOT NULL AND GridColumn IS NOT NULL)
        )
);
```

### Notes

- `Locations` is the single source of truth for rooms, kennels, hallways, isolation spaces, intake, yards, and other spaces.
- `LocationCode` should remain stable for the physical place. Renaming the display `Name` should not require a new row.
- `UQ_Locations_LocationId_Facility` is intentionally redundant with the primary key. It exists so SQL Server can enforce **same-facility parent/child** relationships with a composite foreign key.
- `GridRow` and `GridColumn` are either both populated or both null.
- `StackLevel` defaults to `0` so single-level kennel placement does not need special handling.
- `Kennel` rows are required to have a parent location in MVP.
- The schema does **not** hard-block non-kennel rows from using grid fields, but the application should treat the grid as a kennel-map concern in MVP.

### Parent/child semantics

The schema supports containment, but these rules should still be validated in the app/import pipeline:

- kennels should not have children
- kennels should usually have a room-like parent
- hallways and yards should usually not be containment parents in MVP
- parent chains must not be cyclic

### Recommended filtered unique index for kennel map placement

```sql
CREATE UNIQUE INDEX UX_Locations_ActiveKennelGridPosition
    ON Locations (ParentLocationId, GridRow, GridColumn, StackLevel)
    WHERE LocationType = 'Kennel'
      AND IsActive = 1
      AND ParentLocationId IS NOT NULL
      AND GridRow IS NOT NULL
      AND GridColumn IS NOT NULL;
```

This enforces the acceptance rule that active, placed kennels cannot collide in the same room/grid position.

---

## 3. LocationLinks

```sql
CREATE TABLE LocationLinks (
    LocationLinkId        INT IDENTITY PRIMARY KEY,
    FacilityId            INT           NOT NULL,
    FromLocationId        INT           NOT NULL,
    ToLocationId          INT           NOT NULL,
    LinkType              NVARCHAR(30)  NOT NULL,
    IsActive              BIT           NOT NULL DEFAULT 1,
    SourceType            NVARCHAR(20)  NOT NULL DEFAULT 'Manual',
    SourceReference       NVARCHAR(200) NULL,
    Notes                 NVARCHAR(500) NULL,
    CreatedUtc            DATETIME2     NOT NULL,
    ModifiedUtc           DATETIME2     NOT NULL,


    CONSTRAINT FK_LocationLinks_Facilities
        FOREIGN KEY (FacilityId) REFERENCES Facilities(FacilityId),

    CONSTRAINT FK_LocationLinks_FromLocationSameFacility
        FOREIGN KEY (FromLocationId, FacilityId)
        REFERENCES Locations (LocationId, FacilityId),

    CONSTRAINT FK_LocationLinks_ToLocationSameFacility
        FOREIGN KEY (ToLocationId, FacilityId)
        REFERENCES Locations (LocationId, FacilityId),

    CONSTRAINT CK_LocationLinks_NotSelf
        CHECK (FromLocationId <> ToLocationId),

    CONSTRAINT CK_LocationLinks_LinkType
        CHECK (LinkType IN (
            'AdjacentLeft', 'AdjacentRight',
            'AdjacentAbove', 'AdjacentBelow',
            'AdjacentOther', 'Connected',
            'Airflow', 'TransportPath'
        )),

    CONSTRAINT CK_LocationLinks_SourceType
        CHECK (SourceType IN ('Manual', 'Import', 'Derived'))
);
```

### Notes

- This table stores both kennel adjacency and broader topology relationships.
- The composite foreign keys enforce **no cross-facility links** in MVP.
- All links are stored as **directed rows**.
- Semantically symmetric links such as `Connected`, `Airflow`, `TransportPath`, and `AdjacentOther` should still be persisted as **two rows**, one in each direction.
- There is intentionally **no separate `AdjacencyLinks` table** and **no separate `TopologyLinks` table** for MVP.

### Recommended filtered unique index for active directed links

```sql
CREATE UNIQUE INDEX UX_LocationLinks_ActiveDirected
    ON LocationLinks (FromLocationId, ToLocationId, LinkType)
    WHERE IsActive = 1;
```

This allows inactive historical/corrected rows to exist without blocking an active replacement row.

### Important rule not represented by a table

**Same-room** is derived from containment.

Do not create a `SameRoomLinks` table. When trace logic needs same-room relationships, it should derive them from shared parentage or direct occupancy of the same room-like space.

### Link endpoint validation that should remain in app/import logic

SQL can enforce same facility and no self-links, but the application should enforce the domain-specific endpoint rules:

- adjacency links must connect kennel -> kennel
- topology links should connect space -> space in MVP
- topology links should not connect kennel locations in MVP
- inverse directional pairs should be created and removed consistently

---

## 4. Animals

```sql
CREATE TABLE Animals (
    AnimalId              INT IDENTITY PRIMARY KEY,
    AnimalNumber          NVARCHAR(50)  NOT NULL,
    Name                  NVARCHAR(100) NULL,
    Species               NVARCHAR(20)  NOT NULL DEFAULT 'Dog',
    Sex                   NVARCHAR(20)  NULL,
    Breed                 NVARCHAR(100) NULL,
    DateOfBirth           DATE          NULL,
    IsActive              BIT           NOT NULL DEFAULT 1,
    Notes                 NVARCHAR(500) NULL,
    CreatedUtc            DATETIME2     NOT NULL,
    ModifiedUtc           DATETIME2     NOT NULL,

    CONSTRAINT UQ_Animals_AnimalNumber
        UNIQUE (AnimalNumber)
);
```

### Notes

- Keep this table minimal unless and until a richer external animal system becomes the source of truth.
- `AnimalNumber` is the natural key for import/reconciliation.

---

## 5. MovementEvents

```sql
CREATE TABLE MovementEvents (
    MovementEventId       BIGINT IDENTITY PRIMARY KEY,
    AnimalId              INT           NOT NULL,
    LocationId            INT           NOT NULL,
    StartUtc              DATETIME2     NOT NULL,
    EndUtc                DATETIME2     NULL,
    MovementReason        NVARCHAR(50)  NULL,
    SourceType            NVARCHAR(20)  NOT NULL DEFAULT 'Manual',
    RecordedByUserId      NVARCHAR(450) NULL,
    Notes                 NVARCHAR(500) NULL,
    CreatedUtc            DATETIME2     NOT NULL,
    ModifiedUtc           DATETIME2     NOT NULL,

    CONSTRAINT FK_MovementEvents_Animals
        FOREIGN KEY (AnimalId) REFERENCES Animals(AnimalId),

    CONSTRAINT FK_MovementEvents_Locations
        FOREIGN KEY (LocationId) REFERENCES Locations(LocationId),

    CONSTRAINT CK_MovementEvents_EndAfterStart
        CHECK (EndUtc IS NULL OR EndUtc > StartUtc),

    CONSTRAINT CK_MovementEvents_SourceType
        CHECK (SourceType IN ('Manual', 'Import', 'Derived'))
);
```

### Notes

- A `MovementEvent` row is conceptually a **stay interval**.
- `EndUtc IS NULL` means the stay is open/current.
- Current placement is **derived** from the open row, not duplicated onto the `Animals` table.
- The schema intentionally does **not** restrict multiple animals from occupying the same location at the same time.
- Capacity and single-animal placement rules should be a later, explicit policy layer if needed.

### Half-open interval semantics for tracing

Treat intervals as:

```text
[StartUtc, EndUtc)
```

Meaning:

- `StartUtc` is inclusive
- `EndUtc` is exclusive
- if `EndUtc` is null, the stay is treated as ongoing

### Recommended filtered unique index for current/open stays

```sql
CREATE UNIQUE INDEX UX_MovementEvents_OneOpenStayPerAnimal
    ON MovementEvents (AnimalId)
    WHERE EndUtc IS NULL;
```

This enforces one of the most important domain rules directly in SQL.

### Overlap rule that should be enforced in service logic

SQL Server does not have a clean built-in exclusion constraint for interval overlap, so the application should enforce:

- the same animal cannot have overlapping stays

Recommended overlap predicate:

```text
A.StartUtc < COALESCE(B.EndUtc, 'infinity')
AND
B.StartUtc < COALESCE(A.EndUtc, 'infinity')
```

That predicate is consistent with half-open interval semantics and allows same-timestamp handoff.

### Recommended transaction pattern for movement writes

For manual movement entry or import upsert logic:

1. lock/read the animal's current and nearby stays inside a transaction
2. check for overlap using the half-open predicate
3. close the existing open stay if the animal is moving
4. insert the new stay
5. commit only if the invariant still holds

Do not rely only on UI checks for this.

---

## 6. Diseases

```sql
CREATE TABLE Diseases (
    DiseaseId             INT IDENTITY PRIMARY KEY,
    DiseaseCode           NVARCHAR(50)  NOT NULL,
    Name                  NVARCHAR(100) NOT NULL,
    IsActive              BIT           NOT NULL DEFAULT 1,
    Notes                 NVARCHAR(500) NULL,
    CreatedUtc            DATETIME2     NOT NULL,
    ModifiedUtc           DATETIME2     NOT NULL,

    CONSTRAINT UQ_Diseases_DiseaseCode
        UNIQUE (DiseaseCode)
);
```

---

## 7. DiseaseTraceProfiles

```sql
CREATE TABLE DiseaseTraceProfiles (
    DiseaseTraceProfileId INT IDENTITY PRIMARY KEY,
    DiseaseId             INT           NOT NULL,
    DefaultLookbackHours  INT           NOT NULL,
    IncludeSameLocation   BIT           NOT NULL DEFAULT 1,
    IncludeSameRoom       BIT           NOT NULL DEFAULT 1,
    IncludeAdjacent       BIT           NOT NULL DEFAULT 1,
    AdjacencyDepth        INT           NOT NULL DEFAULT 1,
    IncludeTopologyLinks  BIT           NOT NULL DEFAULT 0,
    TopologyDepth         INT           NOT NULL DEFAULT 0,
    IsActive              BIT           NOT NULL DEFAULT 1,
    Notes                 NVARCHAR(500) NULL,
    CreatedUtc            DATETIME2     NOT NULL,
    ModifiedUtc           DATETIME2     NOT NULL,

    CONSTRAINT FK_DiseaseTraceProfiles_Diseases
        FOREIGN KEY (DiseaseId) REFERENCES Diseases(DiseaseId),

    CONSTRAINT UQ_DiseaseTraceProfiles_Disease
        UNIQUE (DiseaseId),

    CONSTRAINT CK_DiseaseTraceProfiles_Lookback
        CHECK (DefaultLookbackHours > 0),

    CONSTRAINT CK_DiseaseTraceProfiles_AdjacencySettings
        CHECK (
            (IncludeAdjacent = 0 AND AdjacencyDepth = 0)
            OR
            (IncludeAdjacent = 1 AND AdjacencyDepth > 0)
        ),

    CONSTRAINT CK_DiseaseTraceProfiles_TopologySettings
        CHECK (
            (IncludeTopologyLinks = 0 AND TopologyDepth = 0)
            OR
            (IncludeTopologyLinks = 1 AND TopologyDepth > 0)
        )
);
```

### Notes

- For MVP, assume **one profile definition per disease**.
- If you later need historical profile versioning or multiple selectable profiles for one disease, relax `UQ_DiseaseTraceProfiles_Disease` and add an explicit profile name/version concept.
- `IncludeSameRoom` works with containment. It does **not** require storing room-level links.

---

## 8. DiseaseTraceProfileTopologyLinkTypes

```sql
CREATE TABLE DiseaseTraceProfileTopologyLinkTypes (
    DiseaseTraceProfileId INT          NOT NULL,
    LinkType              NVARCHAR(30) NOT NULL,

    CONSTRAINT PK_DiseaseTraceProfileTopologyLinkTypes
        PRIMARY KEY (DiseaseTraceProfileId, LinkType),

    CONSTRAINT FK_DiseaseTraceProfileTopologyLinkTypes_Profile
        FOREIGN KEY (DiseaseTraceProfileId)
        REFERENCES DiseaseTraceProfiles(DiseaseTraceProfileId),

    CONSTRAINT CK_DiseaseTraceProfileTopologyLinkTypes_LinkType
        CHECK (LinkType IN ('Connected', 'Airflow', 'TransportPath'))
);
```

### Notes

- This table is intentionally limited to **topology** link types.
- Adjacency behavior is controlled by `IncludeAdjacent` and `AdjacencyDepth`, not by rows in this table.
- This naming makes the model clearer than the earlier generic `DiseaseTraceProfileLinkTypes` table name.

---

## 9. ImportBatches

```sql
CREATE TABLE ImportBatches (
    ImportBatchId         BIGINT IDENTITY PRIMARY KEY,
    BatchType             NVARCHAR(50)  NOT NULL,
    FacilityId            INT           NULL,
    SourceFileName        NVARCHAR(260) NOT NULL,
    SourceFileHash        NVARCHAR(128) NULL,
    RunMode               NVARCHAR(20)  NOT NULL,
    Status                NVARCHAR(20)  NOT NULL,
    StartedUtc            DATETIME2     NOT NULL,
    CompletedUtc          DATETIME2     NULL,
    ExecutedByUserId      NVARCHAR(450) NULL,
    Summary               NVARCHAR(MAX) NULL,

    CONSTRAINT FK_ImportBatches_Facilities
        FOREIGN KEY (FacilityId) REFERENCES Facilities(FacilityId),

    CONSTRAINT CK_ImportBatches_RunMode
        CHECK (RunMode IN ('ValidateOnly', 'Commit')),

    CONSTRAINT CK_ImportBatches_Status
        CHECK (Status IN ('Pending', 'Failed', 'Succeeded'))
);
```

### Notes

- `FacilityId` may be null for imports that are validated before facility resolution.
- `SourceFileHash` helps detect repeated runs of the same file.
- The batch row gives a durable audit trail for layout migration and correction work.

---

## 10. ImportIssues

```sql
CREATE TABLE ImportIssues (
    ImportIssueId         BIGINT IDENTITY PRIMARY KEY,
    ImportBatchId         BIGINT         NOT NULL,
    Severity              NVARCHAR(10)   NOT NULL,
    SheetName             NVARCHAR(100)  NOT NULL,
    RowNumber             INT            NULL,
    ItemKey               NVARCHAR(200)  NULL,
    Message               NVARCHAR(1000) NOT NULL,

    CONSTRAINT FK_ImportIssues_ImportBatches
        FOREIGN KEY (ImportBatchId) REFERENCES ImportBatches(ImportBatchId),

    CONSTRAINT CK_ImportIssues_Severity
        CHECK (Severity IN ('Error', 'Warning'))
);
```

---

## Optional later tables

These are intentionally out of MVP unless real usage proves the need:

- `TraceRuns`
- `TraceRunImpactedLocations`
- `TraceRunExposedAnimals`
- `AnimalExternalReferences`
- `LocationStatusHistory`
- `AuditEntries`
- effective-dated `LocationLinks` history tables
- capacity / occupancy policy tables

---

## Recommended indexes

## Locations

```sql
CREATE INDEX IX_Locations_Facility_Parent
    ON Locations (FacilityId, ParentLocationId, IsActive, LocationType);

CREATE INDEX IX_Locations_Facility_Type_Active
    ON Locations (FacilityId, LocationType, IsActive);

CREATE INDEX IX_Locations_Parent_DisplayOrder
    ON Locations (ParentLocationId, DisplayOrder, Name);
```

## LocationLinks

```sql
CREATE INDEX IX_LocationLinks_Facility_Type_Active
    ON LocationLinks (FacilityId, LinkType, IsActive);

CREATE INDEX IX_LocationLinks_From_Type_Active
    ON LocationLinks (FromLocationId, LinkType, IsActive);

CREATE INDEX IX_LocationLinks_To_Type_Active
    ON LocationLinks (ToLocationId, LinkType, IsActive);
```

## MovementEvents

```sql
CREATE INDEX IX_MovementEvents_Animal_Start_End
    ON MovementEvents (AnimalId, StartUtc DESC, EndUtc, LocationId);

CREATE INDEX IX_MovementEvents_Location_Start_End
    ON MovementEvents (LocationId, StartUtc, EndUtc, AnimalId);

CREATE INDEX IX_MovementEvents_OpenByLocation
    ON MovementEvents (LocationId, AnimalId)
    WHERE EndUtc IS NULL;
```

## Disease trace tables

```sql
CREATE INDEX IX_DiseaseTraceProfiles_Disease_Active
    ON DiseaseTraceProfiles (DiseaseId, IsActive);
```

---

## What is enforced where

| Rule | Database-enforced | Application / service-enforced | Notes |
|---|---:|---:|---|
| Facility code unique | Yes | No | simple unique constraint |
| Location code unique within facility | Yes | No | simple unique constraint |
| Parent and child in same facility | Yes | Yes | composite self-FK plus app validation |
| Kennel must have a parent | Yes | Yes | simple check constraint plus UI/import validation |
| Parent chains are acyclic | No | Yes | recursive/cycle check belongs in service/import validation |
| Location cannot parent itself | Yes | Yes | direct check plus app sanity checks |
| Active kennel grid slot unique in a room | Yes | Yes | filtered unique index plus UI validation |
| Cross-facility links blocked | Yes | Yes | composite FKs on link endpoints |
| Self-links blocked | Yes | Yes | check constraint plus UI/import validation |
| Duplicate active directed links blocked | Yes | Yes | filtered unique index |
| Adjacency links must be kennel-to-kennel | No | Yes | endpoint type validation |
| Topology links must be space-to-space in MVP | No | Yes | endpoint type validation |
| Reciprocal inverse links created consistently | No | Yes | handled by service/import logic |
| One open stay per animal | Yes | Yes | filtered unique index |
| No overlapping stays for same animal | No | Yes | requires transactional overlap check |
| Same-room is derived, not stored | N/A | Yes | derive from containment in trace logic |

---

## Contact tracing query notes

The schema is intentionally designed so MVP tracing can stay straightforward.

### 1. Resolve the source stays

Identify the seed animal stays or the explicitly selected source stay in the requested time window.

### 2. Expand impacted locations

Use three mechanisms:

- same location: exact `LocationId`
- same room: derived via `ParentLocationId`
- adjacency/topology: traverse `LocationLinks`

### 3. Resolve overlapping stays

For each impacted location, find movement rows whose intervals overlap the relevant exposure window.

If the request includes an optional location scope, apply it as a final filter to the impacted location set using the selected persisted location plus its containment descendants.

When projecting impacted animals, exclude the seed/source animal itself so the result set stays focused on other exposed animals.

### 4. Return reasons, not just IDs

The trace result should include reason codes such as:

- `SameLocation`
- `SameRoom`
- `Adjacent`
- `AirflowLinked`
- `TransportPathLinked`
- `ConnectedSpace`

That rule belongs in the application layer, but this schema is designed to support it directly.

---

## Intentional omissions

These omissions are deliberate and should not be “fixed” by Codex unless requirements change.

### No separate current placement table

Current placement is derived from the one open row in `MovementEvents`.

### No separate room-membership table

Room membership comes from `Locations.ParentLocationId`.

### No separate same-room links

Same-room exposure is derived from containment.

### No one-animal-per-location constraint

The history model allows multiple animals in the same location at the same time.

### No effective-dated topology in MVP

`LocationLinks` represents the current authoritative layout/topology. If retrospective topology history becomes important later, add effective-dated links instead of redesigning the whole model.

---

## Natural keys for import/reconciliation

Use these natural keys for MVP import matching:

- `Facilities`: `FacilityCode`
- `Locations`: `(FacilityCode, LocationCode)`
- `LocationLinks`: `(FacilityCode, FromLocationCode, ToLocationCode, LinkType)`
- `Animals`: `AnimalNumber`
- `Diseases`: `DiseaseCode`

These are important for safe re-runs and deterministic correction workflows.

---

## Summary

This schema supports:

- multi-facility layout
- room/kennel containment
- explicit kennel adjacency
- explicit space topology
- interval-based movement history with clear overlap semantics
- simple disease trace profiles
- re-runnable import batches

The most important practical choices are:

- graph is authoritative for adjacency and topology
- containment is authoritative for same-room logic
- movement history is interval-based and current placement is derived
- occupancy rules stay separate from trace history in MVP

That combination keeps the data model strong enough for tracing without over-engineering the first release.
