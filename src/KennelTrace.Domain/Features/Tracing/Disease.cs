using KennelTrace.Domain.Common;

namespace KennelTrace.Domain.Features.Tracing;

public sealed class Disease
{
    private Disease()
    {
    }

    public Disease(
        DiseaseCode diseaseCode,
        string name,
        DateTime createdUtc,
        DateTime modifiedUtc,
        bool isActive = true,
        string? notes = null)
    {
        DiseaseCode = diseaseCode;
        Name = Guard.RequiredText(name, nameof(name));
        IsActive = isActive;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        CreatedUtc = Guard.RequiredUtc(createdUtc, nameof(createdUtc));
        ModifiedUtc = Guard.RequiredUtc(modifiedUtc, nameof(modifiedUtc));
    }

    public int DiseaseId { get; private set; }

    public DiseaseCode DiseaseCode { get; private set; } = default!;

    public string Name { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public string? Notes { get; private set; }

    public DateTime CreatedUtc { get; private set; }

    public DateTime ModifiedUtc { get; private set; }

    public void Rename(string name, DateTime modifiedUtc)
    {
        Name = Guard.RequiredText(name, nameof(name));
        ModifiedUtc = Guard.RequiredUtc(modifiedUtc, nameof(modifiedUtc));
    }

    public void Deactivate(DateTime modifiedUtc)
    {
        IsActive = false;
        ModifiedUtc = Guard.RequiredUtc(modifiedUtc, nameof(modifiedUtc));
    }
}
