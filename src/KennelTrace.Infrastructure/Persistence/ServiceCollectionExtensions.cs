using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KennelTrace.Infrastructure.Persistence;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKennelTraceSqlServer(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = KennelTraceSqlServerDefaults.GetConnectionString(configuration.GetConnectionString("KennelTrace"));

        services.AddDbContext<KennelTraceDbContext>(options =>
            options.UseSqlServer(connectionString, sqlServer => sqlServer.MigrationsAssembly(typeof(KennelTraceDbContext).Assembly.FullName)));

        return services;
    }
}
