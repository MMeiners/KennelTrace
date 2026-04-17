using KennelTrace.Domain.Common;

namespace KennelTrace.Domain.Features.Tracing;

public sealed class ContactTraceRequest
{
    public ContactTraceRequest(
        int diseaseTraceProfileId,
        DateTime traceWindowStartUtc,
        DateTime traceWindowEndUtc,
        int? sourceAnimalId = null,
        long? sourceStayId = null,
        int? facilityId = null,
        int? locationScopeLocationId = null)
    {
        DiseaseTraceProfileId = Guard.Positive(diseaseTraceProfileId, nameof(diseaseTraceProfileId));
        TraceWindowStartUtc = Guard.RequiredUtc(traceWindowStartUtc, nameof(traceWindowStartUtc));
        TraceWindowEndUtc = Guard.RequiredUtc(traceWindowEndUtc, nameof(traceWindowEndUtc));
        SourceAnimalId = ValidateOptionalPositive(sourceAnimalId, nameof(sourceAnimalId));
        SourceStayId = ValidateOptionalPositive(sourceStayId, nameof(sourceStayId));
        FacilityId = ValidateOptionalPositive(facilityId, nameof(facilityId));
        LocationScopeLocationId = ValidateOptionalPositive(locationScopeLocationId, nameof(locationScopeLocationId));

        Guard.Against(
            (SourceAnimalId is null && SourceStayId is null)
            || (SourceAnimalId is not null && SourceStayId is not null),
            "Exactly one trace source must be specified: sourceAnimalId or sourceStayId.");
        Guard.Against(TraceWindowEndUtc <= TraceWindowStartUtc, "TraceWindowEndUtc must be greater than TraceWindowStartUtc.");
    }

    public int DiseaseTraceProfileId { get; }

    public int? SourceAnimalId { get; }

    public long? SourceStayId { get; }

    public DateTime TraceWindowStartUtc { get; }

    public DateTime TraceWindowEndUtc { get; }

    public int? FacilityId { get; }

    public int? LocationScopeLocationId { get; }

    public bool UsesSourceAnimal => SourceAnimalId is not null;

    public bool UsesSourceStay => SourceStayId is not null;

    private static int? ValidateOptionalPositive(int? value, string paramName) =>
        value is null ? null : Guard.Positive(value.Value, paramName);

    private static long? ValidateOptionalPositive(long? value, string paramName)
    {
        if (value is null)
        {
            return null;
        }

        Guard.Against(value.Value <= 0, $"{paramName} must be greater than zero.");
        return value.Value;
    }
}
