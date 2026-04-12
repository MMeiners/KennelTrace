using KennelTrace.Domain.Common;

namespace KennelTrace.Domain.Features.Locations;

public sealed class Location
{
    private readonly List<Location> _children = [];

    private Location()
    {
    }

    public Location(
        int facilityId,
        LocationType locationType,
        LocationCode locationCode,
        string name,
        DateTime createdUtc,
        DateTime modifiedUtc,
        int? parentLocationId = null,
        bool isActive = true,
        int? gridRow = null,
        int? gridColumn = null,
        int stackLevel = 0,
        int? displayOrder = null,
        string? notes = null)
    {
        Guard.Against(facilityId <= 0, "facilityId is required.");

        FacilityId = facilityId;
        LocationType = locationType;
        LocationCode = locationCode;
        Name = Guard.RequiredText(name, nameof(name));
        CreatedUtc = Guard.RequiredUtc(createdUtc, nameof(createdUtc));
        ModifiedUtc = Guard.RequiredUtc(modifiedUtc, nameof(modifiedUtc));
        IsActive = isActive;
        DisplayOrder = displayOrder;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        SetGridPlacement(gridRow, gridColumn, stackLevel, modifiedUtc);
        SetParent(parentLocationId, modifiedUtc);
    }

    public int LocationId { get; private set; }

    public int FacilityId { get; private set; }

    public int? ParentLocationId { get; private set; }

    public Location? ParentLocation { get; private set; }

    public LocationType LocationType { get; private set; }

    public LocationCode LocationCode { get; private set; } = default!;

    public string Name { get; private set; } = null!;

    public int? GridRow { get; private set; }

    public int? GridColumn { get; private set; }

    public int StackLevel { get; private set; }

    public int? DisplayOrder { get; private set; }

    public bool IsActive { get; private set; }

    public string? Notes { get; private set; }

    public DateTime CreatedUtc { get; private set; }

    public DateTime ModifiedUtc { get; private set; }

    public IReadOnlyCollection<Location> Children => _children;

    public void Rename(string name, DateTime modifiedUtc)
    {
        Name = Guard.RequiredText(name, nameof(name));
        ModifiedUtc = Guard.RequiredUtc(modifiedUtc, nameof(modifiedUtc));
    }

    public void AssignParent(int? parentLocationId, DateTime modifiedUtc)
    {
        SetParent(parentLocationId, modifiedUtc);
    }

    public void SetGridPlacement(int? gridRow, int? gridColumn, int stackLevel, DateTime modifiedUtc)
    {
        Guard.Against((gridRow is null) != (gridColumn is null), "GridRow and GridColumn must both be populated or both be null.");

        GridRow = gridRow is null ? null : Guard.NonNegative(gridRow.Value, nameof(gridRow));
        GridColumn = gridColumn is null ? null : Guard.NonNegative(gridColumn.Value, nameof(gridColumn));
        StackLevel = Guard.NonNegative(stackLevel, nameof(stackLevel));
        ModifiedUtc = Guard.RequiredUtc(modifiedUtc, nameof(modifiedUtc));
    }

    public void SetDisplayOrder(int? displayOrder, DateTime modifiedUtc)
    {
        DisplayOrder = displayOrder;
        ModifiedUtc = Guard.RequiredUtc(modifiedUtc, nameof(modifiedUtc));
    }

    public void SetNotes(string? notes, DateTime modifiedUtc)
    {
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        ModifiedUtc = Guard.RequiredUtc(modifiedUtc, nameof(modifiedUtc));
    }

    public void Deactivate(DateTime modifiedUtc)
    {
        IsActive = false;
        ModifiedUtc = Guard.RequiredUtc(modifiedUtc, nameof(modifiedUtc));
    }

    private void SetParent(int? parentLocationId, DateTime modifiedUtc)
    {
        if (LocationType == LocationType.Kennel)
        {
            Guard.Against(parentLocationId is null, "Kennels must have a valid parent room-like location.");
        }

        ParentLocationId = parentLocationId;
        ModifiedUtc = Guard.RequiredUtc(modifiedUtc, nameof(modifiedUtc));
    }
}
