using KennelTrace.Domain.Common;

namespace KennelTrace.Domain.Features.Facilities;

public sealed class Facility
{
    private Facility()
    {
    }

    public Facility(
        FacilityCode facilityCode,
        string name,
        string timeZoneId,
        DateTime createdUtc,
        DateTime modifiedUtc,
        bool isActive = true,
        string? notes = null)
    {
        FacilityCode = facilityCode;
        Name = Guard.RequiredText(name, nameof(name));
        TimeZoneId = Guard.RequiredText(timeZoneId, nameof(timeZoneId));
        CreatedUtc = Guard.RequiredUtc(createdUtc, nameof(createdUtc));
        ModifiedUtc = Guard.RequiredUtc(modifiedUtc, nameof(modifiedUtc));
        IsActive = isActive;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public int FacilityId { get; private set; }

    public FacilityCode FacilityCode { get; private set; } = default!;

    public string Name { get; private set; } = null!;

    public string TimeZoneId { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public string? Notes { get; private set; }

    public DateTime CreatedUtc { get; private set; }

    public DateTime ModifiedUtc { get; private set; }

    public void Rename(string name, DateTime modifiedUtc)
    {
        Name = Guard.RequiredText(name, nameof(name));
        ModifiedUtc = Guard.RequiredUtc(modifiedUtc, nameof(modifiedUtc));
    }

    public void ApplyImport(string name, string timeZoneId, bool isActive, string? notes, DateTime modifiedUtc)
    {
        Name = Guard.RequiredText(name, nameof(name));
        TimeZoneId = Guard.RequiredText(timeZoneId, nameof(timeZoneId));
        IsActive = isActive;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        ModifiedUtc = Guard.RequiredUtc(modifiedUtc, nameof(modifiedUtc));
    }

    public void UpdateDetails(FacilityCode facilityCode, string name, string timeZoneId, bool isActive, string? notes, DateTime modifiedUtc)
    {
        FacilityCode = facilityCode;
        Name = Guard.RequiredText(name, nameof(name));
        TimeZoneId = Guard.RequiredText(timeZoneId, nameof(timeZoneId));
        IsActive = isActive;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        ModifiedUtc = Guard.RequiredUtc(modifiedUtc, nameof(modifiedUtc));
    }

    public void UpdateTimeZone(string timeZoneId, DateTime modifiedUtc)
    {
        TimeZoneId = Guard.RequiredText(timeZoneId, nameof(timeZoneId));
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
}
