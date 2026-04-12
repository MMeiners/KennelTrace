# Vision

## Product overview

This system is an internal kennel management and contact tracing application for shelter staff.

Its first job is not flashy automation. Its first job is to become the shelter’s trusted source of truth for facility layout, kennel adjacency, room relationships, and animal movement history.

Today that knowledge lives in Word documents and ad hoc notes. That makes layout maintenance slow, inconsistent, and hard to query. The new system should replace that with structured data that staff can read, update, and use for disease tracing.

## Problem statement

The shelter currently has three linked problems:

1. **Facility knowledge is manual and fragile.** Kennel adjacency, room layout, and special relationships such as airflow or transport paths are maintained outside the system.
2. **Movement history is operationally important but hard to use for tracing.** Even if staff know where an animal has been, that information is difficult to connect to nearby kennels and related spaces.
3. **Migration from today’s process must be realistic.** The shelter needs a practical path from messy documents and spreadsheets to a maintained system, without waiting for a perfect import UI.

## Goals

### Primary goals

- Replace Word-based layout and adjacency records with a structured, queryable source of truth.
- Support multiple facilities in one application.
- Model facilities as more than rooms and kennels:
  - rooms
  - hallways
  - medical spaces
  - isolation
  - other staff-relevant areas
- Track animal movement over time in a way that supports overlap-based tracing.
- Run disease/contact trace queries using:
  - source animal
  - source location or stay
  - disease profile
  - time window
  - optional location scope
- Keep MVP simple enough for a small internal team to maintain.
- Prioritize staff adoption over architectural purity.

### Secondary goals

- Make future disease-rule expansion possible without rebuilding the data model.
- Support import from canonical spreadsheets in MVP.
- Make the UI readable for non-technical shelter staff.
- Make missing or incomplete layout data visible instead of hidden.

## Non-goals

These are explicitly **not** MVP goals:

- A full shelter management platform.
- A highly polished drag-and-drop floor-plan editor.
- A generalized low-code rules engine for every disease.
- A self-service end-user import wizard.
- Complex workflow automation before staff can reliably maintain layout and trace data.
- Mobile/offline-first behavior.
- Microservices or distributed architecture.

## Key problems being solved

## 1. Replacing the manual process

The system must stop layout knowledge from living in scattered Word documents and staff memory.

That means the application needs to become the operational source of truth for:

- which kennels exist
- which room each kennel belongs to
- which kennels are adjacent
- which spaces are connected by airflow or transport paths
- which spaces belong to each facility

## 2. Capturing real-world layout without overcomplicating MVP

A simple grid helps staff understand and maintain kennel placement, but the real facility is not always a clean rectangle. Some kennels are stacked or irregular.

The system should therefore support:

- optional grid coordinates for display
- explicit relationship links for true adjacency and topology

The graph is authoritative. The grid is a usability aid.

## 3. Making movement history useful for tracing

Animal movement data should not just be a list of past placements. It should support answering:

- which kennels were directly involved
- which adjacent kennels might be impacted
- which rooms or linked spaces matter
- which animals overlapped those impacted places during the tracing window

## 4. Supporting realistic onboarding

The first release should not depend on perfect data entry workflows. It should support:

- admin/script-driven imports
- validation reports
- safe re-runs
- staged cleanup of messy historical layout data

## Success criteria

The MVP is successful when the following are true:

### Operational success

- Staff can find the current layout and adjacency information in the application instead of relying on Word documents.
- Read-only users can view facility layout, kennel placement, and related spaces without editing access.
- Admins can maintain facility spaces, kennel placement metadata, and adjacency/topology links in the application.

### Data success

- A facility can be loaded from canonical spreadsheet files with validation before commit.
- Each kennel belongs to a valid parent space.
- Explicit adjacency and topology links are stored and queryable.
- Animal movement history is stored as time-bounded location occupancy.

### Tracing success

- Staff can run a trace for a selected animal, disease profile, and time window.
- Results identify impacted locations and animals with a human-readable reason for inclusion.
- Trace logic uses stored relationships and movement history rather than staff memory.

### Adoption success

- The first release reduces dependence on manual documents quickly.
- MVP workflows are understandable without deep technical training.
- The internal team can maintain the system without large architectural ceremony.

## Recommended MVP definition

A good MVP is **not** “everything the final system may one day need.”

A good MVP for this project is:

- multiple facilities
- room/kennel/location management
- explicit relationship links
- read-only map view
- admin editing forms
- movement history
- disease trace query
- admin/script-driven import pipeline

That is enough to replace the manual process and make tracing materially better without overbuilding.
