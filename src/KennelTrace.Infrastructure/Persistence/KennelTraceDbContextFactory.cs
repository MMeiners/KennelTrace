using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace KennelTrace.Infrastructure.Persistence;

public sealed class KennelTraceDbContextFactory : IDesignTimeDbContextFactory<KennelTraceDbContext>
{
    public KennelTraceDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<KennelTraceDbContext>();
        optionsBuilder.UseSqlServer(
            KennelTraceSqlServerDefaults.GetConnectionString(),
            sqlServer => sqlServer.MigrationsAssembly(typeof(KennelTraceDbContext).Assembly.FullName));

        return new KennelTraceDbContext(optionsBuilder.Options);
    }
}
