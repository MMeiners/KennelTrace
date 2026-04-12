# Checkpoint Log

## 2026-04-12 13:40:40 -07:00

Implemented the MVP domain vocabulary and natural-key slice in `KennelTrace.Domain` based on `docs/domain-model.md` and `docs/dev-plan.md`. This added the canonical `LocationType` and `LinkType` enums, shared validation and natural-key primitives, and core entity shells for `Facility`, `Location`, `LocationLink`, `Animal`, `MovementEvent`, `Disease`, `DiseaseTraceProfile`, `ImportBatch`, and `ImportIssue`. The constructors and rule helpers make the current business rules explicit, including kennel parent requirements, same-facility containment, adjacency vs topology endpoint validation, non-negative grid values, and UTC-based movement interval validation.

Replaced the placeholder smoke test with focused unit tests for the invariants added in this slice. The tests cover natural-key validation, kennel containment rules, link validation and inverse mapping, half-open movement interval behavior, trace profile link filtering, and import issue validation. Verified the slice with `dotnet build src/KennelTrace.Domain/KennelTrace.Domain.csproj` and `dotnet test tests/KennelTrace.Tests/KennelTrace.Tests.csproj`, both of which passed.
