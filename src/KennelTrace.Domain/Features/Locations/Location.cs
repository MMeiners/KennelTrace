using KennelTrace.Domain.Common;

namespace KennelTrace.Domain.Features.Locations;

public sealed class Location
{
    public Location(
        Guid id,
        Guid facilityId,
        FacilityCode facilityCode,
        LocationType locationType,
        LocationCode code,
        string displayName,
        Location? parentLocation = null,
        bool isActive = true,
        int? gridRow = null,
        int? gridColumn = null,
        int? stackLevel = null)
    {
        Id = Guard.RequiredId(id, nameof(id));
        FacilityId = Guard.RequiredId(facilityId, nameof(facilityId));
        FacilityCode = facilityCode;
        LocationType = locationType;
        Code = code;
        DisplayName = Guard.RequiredText(displayName, nameof(displayName));
        IsActive = isActive;

        SetGridPlacement(gridRow, gridColumn, stackLevel);
        SetParent(parentLocation);
    }

    public Guid Id { get; }

    public Guid FacilityId { get; }

    public FacilityCode FacilityCode { get; }

    public LocationType LocationType { get; }

    public LocationCode Code { get; }

    public string DisplayName { get; private set; }

    public bool IsActive { get; private set; }

    public Guid? ParentLocationId { get; private set; }

    public int? GridRow { get; private set; }

    public int? GridColumn { get; private set; }

    public int? StackLevel { get; private set; }

    public void Rename(string displayName)
    {
        DisplayName = Guard.RequiredText(displayName, nameof(displayName));
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void AssignParent(Location parentLocation)
    {
        SetParent(parentLocation);
    }

    public void SetGridPlacement(int? gridRow, int? gridColumn, int? stackLevel)
    {
        GridRow = gridRow is null ? null : Guard.NonNegative(gridRow.Value, nameof(gridRow));
        GridColumn = gridColumn is null ? null : Guard.NonNegative(gridColumn.Value, nameof(gridColumn));
        StackLevel = stackLevel is null ? null : Guard.NonNegative(stackLevel.Value, nameof(stackLevel));
    }

    private void SetParent(Location? parentLocation)
    {
        if (LocationType == LocationType.Kennel)
        {
            Guard.Against(parentLocation is null, "Kennels must have a valid parent room-like location.");
        }

        if (parentLocation is null)
        {
            ParentLocationId = null;
            return;
        }

        Guard.Against(parentLocation.Id == Id, "A location cannot be its own parent.");
        Guard.Against(parentLocation.FacilityId != FacilityId, "Parent and child locations must belong to the same facility.");
        Guard.Against(
            !LocationTypeRules.CanContainChild(parentLocation.LocationType, LocationType),
            $"{parentLocation.LocationType} cannot contain {LocationType} locations.");

        ParentLocationId = parentLocation.Id;
    }
}
