using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace FriendDex.Web.Auth;

// Who is signed in right now, for both web pages (cookie) and the app's API calls (token).
public class CurrentUser(IHttpContextAccessor http, AuthenticationStateProvider authState)
{
    public const string UserIdClaim = "frienddex:user_id";

    public async Task<int> GetRequiredIdAsync()
    {
        // API requests and the first page load have an HttpContext with the signed-in user.
        var principal = http.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            // Interactive pages run over a live connection without an HttpContext; Blazor keeps the user here.
            principal = (await authState.GetAuthenticationStateAsync()).User;
        }

        return int.TryParse(principal?.FindFirstValue(UserIdClaim), out var id) && id > 0
            ? id
            : throw new InvalidOperationException("Please log in again.");
    }
}
