using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace KennelTrace.Web.Tests;

internal sealed class TestAuthenticationStateProvider : AuthenticationStateProvider
{
    private AuthenticationState _authenticationState = new(new ClaimsPrincipal(new ClaimsIdentity()));

    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        Task.FromResult(_authenticationState);

    public void SetUser(string userName, params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, userName) };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        _authenticationState = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "test")));
        NotifyAuthenticationStateChanged(Task.FromResult(_authenticationState));
    }
}
