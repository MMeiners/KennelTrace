using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Locations;
using KennelTrace.Domain.Features.Tracing;
using KennelTrace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KennelTrace.Infrastructure.Features.Tracing.ContactTracing;

public sealed class ContactTraceService(KennelTraceDbContext dbContext) : IContactTraceService
{
    private static readonly ImpactedLocationGraphExpander LocationGraphExpander = new();
    private static readonly ImpactedAnimalProjector AnimalProjector = new();

    public async Task<ContactTraceResult> RunAsync(ContactTraceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var profile = await ResolveActiveProfileAsync(request.DiseaseTraceProfileId, cancellationToken);
        var sourceStays = await ResolveSourceStaysAsync(request, cancellationToken);
        var seedAnimalId = ResolveSeedAnimalId(sourceStays);
        var expansionSettings = ImpactedLocationExpansionSettings.FromProfile(profile);
        var traceGraphSnapshot = await LoadTraceGraphSnapshotAsync(sourceStays, expansionSettings, cancellationToken);

        var expandedImpactedLocations = LocationGraphExpander.Expand(new ImpactedLocationExpansionRequest(
            expansionSettings,
            traceGraphSnapshot,
            sourceStays: sourceStays.Select(x => new ResolvedTraceSourceStay(x.StayId, x.LocationId))));

        var scopedLocationIds = request.LocationScopeLocationId is null
            ? null
            : await ResolveScopedLocationIdsAsync(
                request.LocationScopeLocationId.Value,
                request.FacilityId,
                cancellationToken);

        var filteredImpactedLocations = scopedLocationIds is null
            ? expandedImpactedLocations
            : expandedImpactedLocations
                .Where(x => scopedLocationIds.Contains(x.LocationId))
                .ToArray();

        var candidateMovementStays = await LoadCandidateMovementStaysAsync(
            filteredImpactedLocations.Select(x => x.LocationId).ToArray(),
            seedAnimalId,
            request.TraceWindowStartUtc,
            request.TraceWindowEndUtc,
            cancellationToken);

        var impactedAnimals = AnimalProjector.Project(new ImpactedAnimalProjectionRequest(
            request.TraceWindowStartUtc,
            request.TraceWindowEndUtc,
            sourceStays.Select(x => new ResolvedTraceSourceStayInterval(x.StayId, x.LocationId, x.StartUtc, x.EndUtc)),
            filteredImpactedLocations,
            candidateMovementStays));

        var impactedLocations = filteredImpactedLocations
            .Select(MapImpactedLocationResult)
            .ToArray();

        return new ContactTraceResult(
            profile.DiseaseTraceProfileId,
            sourceStays.Select(x => x.StayId),
            impactedLocations,
            impactedAnimals);
    }

    private async Task<DiseaseTraceProfile> ResolveActiveProfileAsync(int diseaseTraceProfileId, CancellationToken cancellationToken)
    {
        var profile = await dbContext.DiseaseTraceProfiles
            .AsNoTracking()
            .Include(x => x.TopologyLinkTypes)
            .SingleOrDefaultAsync(x => x.DiseaseTraceProfileId == diseaseTraceProfileId && x.IsActive, cancellationToken);

        if (profile is null)
        {
            throw new DomainValidationException("Select an active disease trace profile before running a trace.");
        }

        var diseaseIsActive = await dbContext.Diseases
            .AsNoTracking()
            .AnyAsync(x => x.DiseaseId == profile.DiseaseId && x.IsActive, cancellationToken);

        if (!diseaseIsActive)
        {
            throw new DomainValidationException("The selected disease trace profile does not belong to an active disease.");
        }

        return profile;
    }

    private async Task<IReadOnlyList<SourceStayRow>> ResolveSourceStaysAsync(ContactTraceRequest request, CancellationToken cancellationToken)
    {
        if (request.UsesSourceAnimal)
        {
            var sourceAnimalStayRows = await (
                    from movement in dbContext.MovementEvents.AsNoTracking()
                    join location in dbContext.Locations.AsNoTracking()
                        on movement.LocationId equals location.LocationId
                    where movement.AnimalId == request.SourceAnimalId!.Value
                          && movement.StartUtc < request.TraceWindowEndUtc
                          && (movement.EndUtc == null || request.TraceWindowStartUtc < movement.EndUtc)
                          && (request.FacilityId == null || location.FacilityId == request.FacilityId.Value)
                    orderby movement.StartUtc, movement.EndUtc, movement.MovementEventId
                    select new
                    {
                        StayId = movement.MovementEventId,
                        movement.AnimalId,
                        movement.LocationId,
                        location.FacilityId,
                        movement.StartUtc,
                        movement.EndUtc
                    })
                .ToListAsync(cancellationToken);

            var sourceAnimalStays = sourceAnimalStayRows
                .Select(x => new SourceStayRow(
                    x.StayId,
                    x.AnimalId,
                    x.LocationId,
                    x.FacilityId,
                    NormalizeUtc(x.StartUtc),
                    NormalizeUtc(x.EndUtc)))
                .ToArray();

            if (sourceAnimalStays.Length == 0)
            {
                throw new DomainValidationException(
                    "No source stays were found for the selected animal in the requested trace window.");
            }

            return sourceAnimalStays;
        }

        var explicitSourceStayRow = await (
                from movement in dbContext.MovementEvents.AsNoTracking()
                join location in dbContext.Locations.AsNoTracking()
                    on movement.LocationId equals location.LocationId
                where movement.MovementEventId == request.SourceStayId!.Value
                select new
                {
                    StayId = movement.MovementEventId,
                    movement.AnimalId,
                    movement.LocationId,
                    location.FacilityId,
                    movement.StartUtc,
                    movement.EndUtc
                })
            .SingleOrDefaultAsync(cancellationToken);

        var explicitSourceStay = explicitSourceStayRow is null
            ? null
            : new SourceStayRow(
                explicitSourceStayRow.StayId,
                explicitSourceStayRow.AnimalId,
                explicitSourceStayRow.LocationId,
                explicitSourceStayRow.FacilityId,
                NormalizeUtc(explicitSourceStayRow.StartUtc),
                NormalizeUtc(explicitSourceStayRow.EndUtc));

        if (explicitSourceStay is null)
        {
            throw new DomainValidationException("Select a valid persisted source stay before running a trace.");
        }

        if (request.FacilityId is not null && explicitSourceStay.FacilityId != request.FacilityId.Value)
        {
            throw new DomainValidationException("The selected source stay does not belong to the requested facility.");
        }

        if (explicitSourceStay.StartUtc >= request.TraceWindowEndUtc
            || (explicitSourceStay.EndUtc is not null && request.TraceWindowStartUtc >= explicitSourceStay.EndUtc.Value))
        {
            throw new DomainValidationException("The selected source stay does not overlap the requested trace window.");
        }

        return [explicitSourceStay];
    }

    private async Task<TraceGraphSnapshot> LoadTraceGraphSnapshotAsync(
        IReadOnlyList<SourceStayRow> sourceStays,
        ImpactedLocationExpansionSettings settings,
        CancellationToken cancellationToken)
    {
        var locationsById = new Dictionary<int, TraceGraphLocation>();
        var linksByKey = new Dictionary<(int FromLocationId, int ToLocationId, LinkType LinkType), TraceGraphLink>();

        await EnsureLocationsLoadedAsync(
            sourceStays.Select(x => x.LocationId).Distinct().ToArray(),
            locationsById,
            cancellationToken);
        await EnsureParentLocationsLoadedAsync(locationsById, cancellationToken);

        var sourceScopeLocationIds = ResolveSameRoomScopeLocationIds(sourceStays, locationsById);
        await EnsureChildLocationsLoadedAsync(sourceScopeLocationIds, locationsById, cancellationToken);

        if (settings.HasAdjacencyTraversal)
        {
            var adjacencyLinkTypes = Enum.GetValues<LinkType>()
                .Where(LinkTypeRules.IsAdjacency)
                .ToArray();

            var frontierLocationIds = sourceStays
                .Select(x => x.LocationId)
                .Distinct()
                .ToArray();

            for (var depth = 0; depth < settings.AdjacencyDepth && frontierLocationIds.Length > 0; depth++)
            {
                var outgoingLinks = await LoadLinksAsync(frontierLocationIds, adjacencyLinkTypes, cancellationToken);
                AddLinks(outgoingLinks, linksByKey);

                frontierLocationIds = outgoingLinks
                    .Select(x => x.ToLocationId)
                    .Distinct()
                    .ToArray();

                await EnsureLocationsLoadedAsync(frontierLocationIds, locationsById, cancellationToken);
            }
        }

        if (settings.HasTopologyTraversal)
        {
            var frontierLocationIds = sourceStays
                .Select(x => x.LocationId)
                .Concat(sourceScopeLocationIds)
                .Distinct()
                .ToArray();

            for (var depth = 0; depth < settings.TopologyDepth && frontierLocationIds.Length > 0; depth++)
            {
                var outgoingLinks = await LoadLinksAsync(frontierLocationIds, settings.AllowedTopologyLinkTypes, cancellationToken);
                AddLinks(outgoingLinks, linksByKey);

                frontierLocationIds = outgoingLinks
                    .Select(x => x.ToLocationId)
                    .Distinct()
                    .ToArray();

                await EnsureLocationsLoadedAsync(frontierLocationIds, locationsById, cancellationToken);

                var roomLikeDestinationIds = frontierLocationIds
                    .Where(x => locationsById.TryGetValue(x, out var location) && LocationTypeRules.IsRoomLike(location.LocationType))
                    .ToArray();

                await EnsureChildLocationsLoadedAsync(roomLikeDestinationIds, locationsById, cancellationToken);
            }
        }

        return new TraceGraphSnapshot(locationsById.Values, linksByKey.Values);
    }

    private async Task<HashSet<int>> ResolveScopedLocationIdsAsync(
        int locationScopeLocationId,
        int? facilityId,
        CancellationToken cancellationToken)
    {
        var scopeLocation = await dbContext.Locations
            .AsNoTracking()
            .Where(x => x.LocationId == locationScopeLocationId)
            .Select(x => new { x.LocationId, x.FacilityId })
            .SingleOrDefaultAsync(cancellationToken);

        if (scopeLocation is null)
        {
            throw new DomainValidationException("Select a valid persisted location scope before running a trace.");
        }

        if (facilityId is not null && scopeLocation.FacilityId != facilityId.Value)
        {
            throw new DomainValidationException("The selected location scope does not belong to the requested facility.");
        }

        var scopedLocationIds = new HashSet<int> { scopeLocation.LocationId };
        var frontierLocationIds = new[] { scopeLocation.LocationId };

        while (frontierLocationIds.Length > 0)
        {
            var childLocationIds = await dbContext.Locations
                .AsNoTracking()
                .Where(x => x.ParentLocationId != null && frontierLocationIds.Contains(x.ParentLocationId.Value))
                .Select(x => x.LocationId)
                .ToListAsync(cancellationToken);

            frontierLocationIds = childLocationIds
                .Where(scopedLocationIds.Add)
                .ToArray();
        }

        return scopedLocationIds;
    }

    private async Task<IReadOnlyList<TraceCandidateMovementStay>> LoadCandidateMovementStaysAsync(
        IReadOnlyCollection<int> impactedLocationIds,
        int seedAnimalId,
        DateTime traceWindowStartUtc,
        DateTime traceWindowEndUtc,
        CancellationToken cancellationToken)
    {
        if (impactedLocationIds.Count == 0)
        {
            return [];
        }

        var candidateMovementStayRows = await (
                from movement in dbContext.MovementEvents.AsNoTracking()
                join animal in dbContext.Animals.AsNoTracking()
                    on movement.AnimalId equals animal.AnimalId
                where impactedLocationIds.Contains(movement.LocationId)
                      && movement.AnimalId != seedAnimalId
                      && movement.StartUtc < traceWindowEndUtc
                      && (movement.EndUtc == null || traceWindowStartUtc < movement.EndUtc)
                orderby animal.AnimalNumber, animal.Name, movement.AnimalId, movement.StartUtc, movement.MovementEventId
                select new
                {
                    StayId = movement.MovementEventId,
                    movement.AnimalId,
                    animal.AnimalNumber,
                    movement.LocationId,
                    movement.StartUtc,
                    movement.EndUtc,
                    animal.Name
                })
            .ToListAsync(cancellationToken);

        return candidateMovementStayRows
            .Select(x => new TraceCandidateMovementStay(
                x.StayId,
                x.AnimalId,
                x.AnimalNumber,
                x.LocationId,
                NormalizeUtc(x.StartUtc),
                NormalizeUtc(x.EndUtc),
                x.Name))
            .ToArray();
    }

    private async Task EnsureLocationsLoadedAsync(
        IReadOnlyCollection<int> locationIds,
        IDictionary<int, TraceGraphLocation> locationsById,
        CancellationToken cancellationToken)
    {
        var missingLocationIds = locationIds
            .Where(x => !locationsById.ContainsKey(x))
            .Distinct()
            .ToArray();

        if (missingLocationIds.Length == 0)
        {
            return;
        }

        var loadedLocations = await dbContext.Locations
            .AsNoTracking()
            .Where(x => missingLocationIds.Contains(x.LocationId))
            .Select(x => new TraceGraphLocation(
                x.LocationId,
                x.LocationType,
                x.ParentLocationId,
                x.GridRow,
                x.GridColumn,
                x.StackLevel))
            .ToListAsync(cancellationToken);

        foreach (var loadedLocation in loadedLocations)
        {
            locationsById[loadedLocation.LocationId] = loadedLocation;
        }
    }

    private async Task EnsureParentLocationsLoadedAsync(
        IDictionary<int, TraceGraphLocation> locationsById,
        CancellationToken cancellationToken)
    {
        var parentLocationIds = locationsById.Values
            .Where(x => x.ParentLocationId is not null && !locationsById.ContainsKey(x.ParentLocationId.Value))
            .Select(x => x.ParentLocationId!.Value)
            .Distinct()
            .ToArray();

        await EnsureLocationsLoadedAsync(parentLocationIds, locationsById, cancellationToken);
    }

    private async Task EnsureChildLocationsLoadedAsync(
        IReadOnlyCollection<int> parentLocationIds,
        IDictionary<int, TraceGraphLocation> locationsById,
        CancellationToken cancellationToken)
    {
        if (parentLocationIds.Count == 0)
        {
            return;
        }

        var childLocations = await dbContext.Locations
            .AsNoTracking()
            .Where(x => x.ParentLocationId != null && parentLocationIds.Contains(x.ParentLocationId.Value))
            .Select(x => new TraceGraphLocation(
                x.LocationId,
                x.LocationType,
                x.ParentLocationId,
                x.GridRow,
                x.GridColumn,
                x.StackLevel))
            .ToListAsync(cancellationToken);

        foreach (var childLocation in childLocations)
        {
            locationsById[childLocation.LocationId] = childLocation;
        }
    }

    private async Task<IReadOnlyList<TraceGraphLink>> LoadLinksAsync(
        IReadOnlyCollection<int> fromLocationIds,
        IReadOnlyCollection<LinkType> linkTypes,
        CancellationToken cancellationToken)
    {
        if (fromLocationIds.Count == 0 || linkTypes.Count == 0)
        {
            return [];
        }

        return await dbContext.LocationLinks
            .AsNoTracking()
            .Where(x => x.IsActive
                        && fromLocationIds.Contains(x.FromLocationId)
                        && linkTypes.Contains(x.LinkType))
            .Select(x => new TraceGraphLink(x.FromLocationId, x.ToLocationId, x.LinkType))
            .ToListAsync(cancellationToken);
    }

    private static int ResolveSeedAnimalId(IReadOnlyList<SourceStayRow> sourceStays)
    {
        var distinctAnimalIds = sourceStays
            .Select(x => x.AnimalId)
            .Distinct()
            .ToArray();

        if (distinctAnimalIds.Length != 1)
        {
            throw new DomainValidationException("Resolved source stays must all belong to the same source animal.");
        }

        return distinctAnimalIds[0];
    }

    private static int[] ResolveSameRoomScopeLocationIds(
        IReadOnlyList<SourceStayRow> sourceStays,
        IReadOnlyDictionary<int, TraceGraphLocation> locationsById)
    {
        return sourceStays
            .Select(x => ResolveSameRoomScopeLocationId(x.LocationId, locationsById))
            .OfType<int>()
            .Distinct()
            .OrderBy(x => x)
            .ToArray();
    }

    private static int? ResolveSameRoomScopeLocationId(
        int sourceLocationId,
        IReadOnlyDictionary<int, TraceGraphLocation> locationsById)
    {
        if (!locationsById.TryGetValue(sourceLocationId, out var sourceLocation))
        {
            return null;
        }

        if (LocationTypeRules.IsRoomLike(sourceLocation.LocationType))
        {
            return sourceLocation.LocationId;
        }

        if (sourceLocation.ParentLocationId is null)
        {
            return null;
        }

        if (locationsById.TryGetValue(sourceLocation.ParentLocationId.Value, out var parentLocation))
        {
            return LocationTypeRules.IsRoomLike(parentLocation.LocationType)
                ? parentLocation.LocationId
                : null;
        }

        return sourceLocation.LocationType == LocationType.Kennel
            ? sourceLocation.ParentLocationId.Value
            : null;
    }

    private static void AddLinks(
        IEnumerable<TraceGraphLink> links,
        IDictionary<(int FromLocationId, int ToLocationId, LinkType LinkType), TraceGraphLink> linksByKey)
    {
        foreach (var link in links)
        {
            linksByKey[(link.FromLocationId, link.ToLocationId, link.LinkType)] = link;
        }
    }

    private static ImpactedLocationResult MapImpactedLocationResult(ExpandedImpactedLocation expandedLocation)
    {
        var primaryReason = expandedLocation.Reasons[0];

        return new ImpactedLocationResult(
            expandedLocation.LocationId,
            expandedLocation.ReasonCodes,
            primaryReason.MatchKind,
            primaryReason.ScopeLocationId,
            primaryReason.TraversalDepth,
            primaryReason.ViaLinkType);
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime? NormalizeUtc(DateTime? value) =>
        value is null ? null : NormalizeUtc(value.Value);

    private sealed record SourceStayRow(
        long StayId,
        int AnimalId,
        int LocationId,
        int FacilityId,
        DateTime StartUtc,
        DateTime? EndUtc);
}
