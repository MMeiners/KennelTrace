namespace KennelTrace.Infrastructure.Persistence;

internal static class KennelTraceSqlServerDefaults
{
    private const string DefaultConnectionString = "Server=localhost;Database=KennelTrace;Integrated Security=true;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True";

    public static string GetConnectionString(string? configuredConnectionString = null)
    {
        if (!string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            return configuredConnectionString;
        }

        var environmentConnectionString = Environment.GetEnvironmentVariable("KENNELTRACE_CONNECTION_STRING");
        return string.IsNullOrWhiteSpace(environmentConnectionString)
            ? DefaultConnectionString
            : environmentConnectionString;
    }
}
