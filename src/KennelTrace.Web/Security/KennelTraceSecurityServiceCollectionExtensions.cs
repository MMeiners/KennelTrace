namespace KennelTrace.Web.Security;

public static class KennelTraceSecurityServiceCollectionExtensions
{
    public static IServiceCollection AddKennelTraceSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCascadingAuthenticationState();
        services.AddAuthorizationBuilder()
            .AddPolicy(KennelTracePolicies.AdminOnly, policy => policy.RequireRole(KennelTraceRoles.Admin));

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = DevelopmentRoleAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = DevelopmentRoleAuthenticationHandler.SchemeName;
            })
            .AddScheme<DevelopmentRoleAuthenticationOptions, DevelopmentRoleAuthenticationHandler>(
                DevelopmentRoleAuthenticationHandler.SchemeName,
                options =>
                {
                    configuration.GetSection(DevelopmentRoleAuthenticationOptions.ConfigurationSectionName)
                        .Bind(options);
                });

        return services;
    }
}
