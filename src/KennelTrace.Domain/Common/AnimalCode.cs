namespace KennelTrace.Domain.Common;

public readonly record struct AnimalCode
{
    public AnimalCode(string value)
    {
        Value = Guard.RequiredText(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}
