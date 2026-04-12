namespace KennelTrace.Domain.Common;

public static class Guard
{
    public static Guid RequiredId(Guid value, string paramName)
    {
        if (value == Guid.Empty)
        {
            throw new DomainValidationException($"{paramName} is required.");
        }

        return value;
    }

    public static string RequiredText(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException($"{paramName} is required.");
        }

        return value.Trim();
    }

    public static DateTime RequiredUtc(DateTime value, string paramName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new DomainValidationException($"{paramName} must use UTC.");
        }

        return value;
    }

    public static int NonNegative(int value, string paramName)
    {
        if (value < 0)
        {
            throw new DomainValidationException($"{paramName} cannot be negative.");
        }

        return value;
    }

    public static int Positive(int value, string paramName)
    {
        if (value <= 0)
        {
            throw new DomainValidationException($"{paramName} must be greater than zero.");
        }

        return value;
    }

    public static void Against(bool condition, string message)
    {
        if (condition)
        {
            throw new DomainValidationException(message);
        }
    }
}
