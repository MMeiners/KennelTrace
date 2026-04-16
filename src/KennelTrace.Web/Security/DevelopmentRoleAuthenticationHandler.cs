using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace KennelTrace.Web.Security;

public sealed class DevelopmentRoleAuthenticationHandler : AuthenticationHandler<DevelopmentRoleAuthenticationOptions>
{
    public const string SchemeName = "DevelopmentRoleSimulation";

    private readonly IWebHostEnvironment _environment;

    public DevelopmentRoleAuthenticationHandler(
        IOptionsMonitor<DevelopmentRoleAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IWebHostEnvironment environment)
        : base(options, logger, encoder)
    {
        _environment = environment;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!_environment.IsDevelopment() || !Options.IsEnabled)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, Options.UserName),
            new(ClaimTypes.NameIdentifier, Options.UserName)
        };

        claims.AddRange(Options.Roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => new Claim(ClaimTypes.Role, role.Trim())));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
