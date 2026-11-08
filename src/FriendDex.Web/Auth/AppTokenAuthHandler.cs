using System.Security.Claims;
using System.Text.Encodings.Web;
using FriendDex.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace FriendDex.Web.Auth;

// Signs in API requests from the Android app using "Authorization: Bearer <token>".
public class AppTokenAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AccountService accounts) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "AppToken";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return AuthenticateResult.NoResult();

        var userId = await accounts.FindUserIdByTokenAsync(header["Bearer ".Length..].Trim());
        if (userId == null) return AuthenticateResult.Fail("Unknown or revoked token.");

        var identity = new ClaimsIdentity([new Claim(CurrentUser.UserIdClaim, userId.Value.ToString())], SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/json";
        return Response.WriteAsync("{\"error\":\"Your login has expired. Log in again.\"}");
    }
}
