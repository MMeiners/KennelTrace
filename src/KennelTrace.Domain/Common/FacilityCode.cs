namespace KennelTrace.Domain.Common;

public readonly record struct FacilityCode
{
    public FacilityCode(string value)
    {
        Value = Guard.RequiredText(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}
