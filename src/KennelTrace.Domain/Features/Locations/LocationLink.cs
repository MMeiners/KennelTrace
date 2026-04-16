using KennelTrace.Domain.Common;

namespace KennelTrace.Domain.Features.Locations;

public sealed class LocationLink
{
    private LocationLink()
    {
    }

    public LocationLink(
        int facilityId,
        int fromLocationId,
        int toLocationId,
        LinkType linkType,
        DateTime createdUtc,
        DateTime modifiedUtc,
        bool isActive = true,
        SourceType sourceType = SourceType.Manual,
        string? sourceReference = null,
        string? notes = null)
    {
        Guard.Against(facilityId <= 0, "facilityId is required.");
        Guard.Against(fromLocationId <= 0, "fromLocationId is required.");
        Guard.Against(toLocationId <= 0, "toLocationId is required.");
        Guard.Against(fromLocationId == toLocationId, "Self-links are invalid.");

        FacilityId = facilityId;
        FromLocationId = fromLocationId;
        ToLocationId = toLocationId;
        LinkType = linkType;
        IsActive = isActive;
        SourceType = sourceType;
        SourceReference = string.IsNullOrWhiteSpace(sourceReference) ? null : sourceReference.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        CreatedUtc = Guard.RequiredUtc(createdUtc, nameof(createdUtc));
        ModifiedUtc = Guard.RequiredUtc(modifiedUtc, nameof(modifiedUtc));
    }

    public int LocationLinkId { get; private set; }

    public int FacilityId { get; private set; }

    public int FromLocationId { get; private set; }

    public int ToLocationId { get; private set; }

    public LinkType LinkType { get; private set; }

    public bool IsActive { get; private set; }

    public SourceType SourceType { get; private set; }

    public string? SourceReference { get; private set; }

    public string? Notes { get; private set; }

    public DateTime CreatedUtc { get; private set; }

    public DateTime ModifiedUtc { get; private set; }

    public void Deactivate(DateTime modifiedUtc)
    {
        IsActive = false;
        ModifiedUtc = Guard.RequiredUtc(modifiedUtc, nameof(modifiedUtc));
    }

    public void Activate(SourceType sourceType, string? sourceReference, string? notes, DateTime modifiedUtc)
    {
        IsActive = true;
        SourceType = sourceType;
        SourceReference = string.IsNullOrWhiteSpace(sourceReference) ? null : sourceReference.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        ModifiedUtc = Guard.RequiredUtc(modifiedUtc, nameof(modifiedUtc));
    }

    public void ApplyImport(SourceType sourceType, string? sourceReference, string? notes, DateTime modifiedUtc)
    {
        Activate(sourceType, sourceReference, notes, modifiedUtc);
    }

    public void UpdateNotes(string? notes, DateTime modifiedUtc)
    {
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        ModifiedUtc = Guard.RequiredUtc(modifiedUtc, nameof(modifiedUtc));
    }
}
