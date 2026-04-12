# Features

This document breaks the product into **MVP**, **Phase 2**, and **Phase 3**.

The ordering is intentionally practical: the first release should replace the manual process quickly and establish a trustworthy data foundation before adding polish.

## MVP

## 1. Multi-facility setup and location registry

**Description**

Support multiple facilities, each with its own room-like spaces and kennels. Admins can maintain facilities and locations; read-only users can browse them.

**Why it matters**

The application cannot replace the manual process unless it can model the real physical structure of the shelter across facilities.

**Dependencies**

- facility/location data model
- role-based authorization
- basic admin forms

## 2. Kennel map data with simple grid placement

**Description**

Store optional display coordinates for kennels within a room so staff can view a simple kennel map. Support irregular layouts by allowing missing or partial grid placement.

**Why it matters**

Staff need a familiar visual representation. A simple grid gives immediate usability without requiring a drag-and-drop designer.

**Dependencies**

- location model
- kennel location type
- map rendering UI
- import support for kennel placement fields

## 3. Explicit adjacency and facility topology management

**Description**

Store trace-relevant links explicitly rather than inferring them from the map. This includes kennel adjacency and broader location links such as connected rooms, airflow, and transport paths.

**Why it matters**

This directly replaces the Word-based relationship notes and creates the queryable source of truth required for tracing.

**Dependencies**

- location model
- location link model
- admin editing UI
- validation rules
- import pipeline

## 4. Read-only facility/kennel map view

**Description**

Provide a browseable view where staff can select a facility and room, see kennel placement, and inspect selected kennels and linked context.

**Why it matters**

The system only replaces manual documents if staff can quickly look up current layout information without editing anything.

**Dependencies**

- facility/location data
- grid metadata
- link data
- read-only authorization path

## 5. Animal records and movement history

**Description**

Record where an animal has been over time using interval-based movement history. Support current placement and prior stays.

**Why it matters**

Movement history is the backbone of any meaningful trace. Without time-bounded occupancy, tracing becomes guesswork.

**Dependencies**

- animal model
- movement event model
- location model
- movement validation rules
- animal detail UI

## 6. Disease profiles and contact tracing

**Description**

Allow staff to run a trace using a selected animal, disease profile, and time window. Return impacted locations and animals with reasons such as same kennel, same room, adjacent kennel, airflow link, or transport path.

**Why it matters**

This is the core operational payoff of the system. It turns structured layout + movement data into staff-usable tracing results.

**Dependencies**

- movement history
- location links
- disease trace profile model
- trace service
- results UI

## 7. Admin/script-driven import and migration pipeline

**Description**

Define and support canonical spreadsheet imports for room-like spaces, kennels, and adjacency/topology links. Validate before commit and support re-runs.

**Why it matters**

This is critical for replacing the current manual process quickly. The shelter should not need to hand-enter every layout detail through the UI before the system becomes useful.

**Dependencies**

- canonical file format
- staging/validation workflow
- natural keys
- import logging
- admin operational process

## 8. Basic authorization and audit metadata

**Description**

Support at least `ReadOnly` and `Admin` roles. Record who changed core operational data where practical.

**Why it matters**

The system is internal, but layout and trace-related data still need controlled editing and basic accountability.

**Dependencies**

- authentication setup
- authorization policies
- data change metadata

## Phase 2

## 1. UI-guided import experience

**Description**

Add an admin upload screen that validates canonical files and shows row-level errors before commit.

**Why it matters**

This improves operational independence after the MVP has proven the import format.

**Dependencies**

- stable MVP import pipeline
- validation reporting
- admin UI work

## 2. Richer trace explainability and saved trace runs

**Description**

Allow staff to save, revisit, and export trace results, including the reason path for each impacted result.

**Why it matters**

Saved trace runs improve repeatability, discussion, and follow-up work during disease incidents.

**Dependencies**

- MVP trace service
- trace snapshot model
- export/reporting UI

## 3. Bulk movement entry / operational shortcuts

**Description**

Add faster workflows for high-volume movement updates, such as bulk transfer or simplified placement entry.

**Why it matters**

Once staff trust the system, speed of daily use becomes more important.

**Dependencies**

- stable movement model
- user feedback from MVP usage

## 4. Better visual editing of maps/topology

**Description**

Add richer visual editing for kennel placement or topology if form-based editing proves too slow.

**Why it matters**

Useful only after the shelter has proven what parts of editing are truly painful.

**Dependencies**

- stable layout model
- real user feedback
- map interaction work

## 5. More flexible disease profile configuration

**Description**

Allow more admin-configurable tracing settings, exclusions, and profile tuning.

**Why it matters**

Different disease workflows may emerge once staff actively use tracing.

**Dependencies**

- MVP disease profile model
- real operational scenarios

## Phase 3

## 1. Integration with external shelter systems

**Description**

Sync animal master data, intake/discharge, or movement events from another system if one becomes authoritative.

**Why it matters**

Reduces duplicate entry and improves consistency across shelter operations.

**Dependencies**

- stable internal data model
- identified source systems
- integration budget/time

## 2. Advanced analytics and risk dashboards

**Description**

Add reporting such as movement heat maps, frequently impacted spaces, or trace trend summaries.

**Why it matters**

Useful after the shelter has enough data volume and staff already trust the core workflows.

**Dependencies**

- stable historical data
- saved trace runs or reporting layer

## 3. Automated alerts and workflow prompts

**Description**

Flag potentially impacted locations or animals automatically based on disease events.

**Why it matters**

Can reduce manual follow-up, but only after trace rules and staff trust are mature.

**Dependencies**

- mature disease profiles
- stable trace logic
- alerting requirements

## 4. Mobile-optimized workflows

**Description**

Optimize for floor staff using phones or tablets for movement entry and lookup.

**Why it matters**

May improve daily operational adoption later, but it is not required to make MVP valuable.

**Dependencies**

- proven core workflows
- device usage feedback

## Feature prioritization summary

If you need the shortest path to value, the true MVP sequence is:

1. facility/location foundation
2. import canonical layout data
3. read-only map and lookup
4. adjacency/topology maintenance
5. animal movement history
6. disease trace query

That is the smallest slice that meaningfully replaces the Word-based process and turns the layout into something operationally useful.
