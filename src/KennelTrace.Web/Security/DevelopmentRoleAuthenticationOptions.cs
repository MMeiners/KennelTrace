using Microsoft.AspNetCore.Authentication;

namespace KennelTrace.Web.Security;

public sealed class DevelopmentRoleAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string ConfigurationSectionName = "Authentication:DevelopmentRoleSimulation";

    public bool IsEnabled { get; set; }

    public string UserName { get; set; } = "dev-admin";

    public string[] Roles { get; set; } = [];
}
