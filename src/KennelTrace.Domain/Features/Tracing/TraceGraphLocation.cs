using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Locations;

namespace KennelTrace.Domain.Features.Tracing;

public sealed class TraceGraphLocation
{
    public TraceGraphLocation(
        int locationId,
        LocationType locationType,
        int? parentLocationId = null,
        int? gridRow = null,
        int? gridColumn = null,
        int stackLevel = 0)
    {
        LocationId = Guard.Positive(locationId, nameof(locationId));
        LocationType = locationType;

        if (parentLocationId is not null)
        {
            ParentLocationId = Guard.Positive(parentLocationId.Value, nameof(parentLocationId));
            Guard.Against(ParentLocationId == LocationId, "A trace graph location cannot parent itself.");
        }

        Guard.Against((gridRow is null) != (gridColumn is null), "GridRow and GridColumn must both be populated or both be null.");

        GridRow = gridRow is null ? null : Guard.NonNegative(gridRow.Value, nameof(gridRow));
        GridColumn = gridColumn is null ? null : Guard.NonNegative(gridColumn.Value, nameof(gridColumn));
        StackLevel = Guard.NonNegative(stackLevel, nameof(stackLevel));
    }

    public int LocationId { get; }

    public LocationType LocationType { get; }

    public int? ParentLocationId { get; }

    public int? GridRow { get; }

    public int? GridColumn { get; }

    public int StackLevel { get; }
}
