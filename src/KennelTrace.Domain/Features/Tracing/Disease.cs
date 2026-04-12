using KennelTrace.Domain.Common;

namespace KennelTrace.Domain.Features.Tracing;

public sealed class Disease
{
    public Disease(Guid id, DiseaseCode code, string displayName, bool isActive = true, string? notes = null)
    {
        Id = Guard.RequiredId(id, nameof(id));
        Code = code;
        DisplayName = Guard.RequiredText(displayName, nameof(displayName));
        IsActive = isActive;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public Guid Id { get; }

    public DiseaseCode Code { get; }

    public string DisplayName { get; private set; }

    public bool IsActive { get; private set; }

    public string? Notes { get; private set; }
}
