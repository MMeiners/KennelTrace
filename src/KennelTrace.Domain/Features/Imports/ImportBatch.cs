using KennelTrace.Domain.Common;

namespace KennelTrace.Domain.Features.Imports;

public sealed class ImportBatch
{
    public ImportBatch(
        Guid id,
        Guid? facilityId,
        string sourceName,
        DateTime startedUtc,
        bool isValidationOnly)
    {
        Id = Guard.RequiredId(id, nameof(id));
        FacilityId = facilityId;
        SourceName = Guard.RequiredText(sourceName, nameof(sourceName));
        StartedUtc = Guard.RequiredUtc(startedUtc, nameof(startedUtc));
        IsValidationOnly = isValidationOnly;
    }

    public Guid Id { get; }

    public Guid? FacilityId { get; }

    public string SourceName { get; }

    public DateTime StartedUtc { get; }

    public DateTime? CompletedUtc { get; private set; }

    public bool IsValidationOnly { get; }

    public bool Succeeded { get; private set; }

    public void Complete(DateTime completedUtc, bool succeeded)
    {
        CompletedUtc = Guard.RequiredUtc(completedUtc, nameof(completedUtc));
        Guard.Against(CompletedUtc < StartedUtc, "CompletedUtc cannot be earlier than StartedUtc.");
        Succeeded = succeeded;
    }
}
