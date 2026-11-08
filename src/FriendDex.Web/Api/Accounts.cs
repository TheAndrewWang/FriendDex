using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using FriendDex.Web.Auth;
using FriendDex.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;

namespace FriendDex.Web.Api;

// Accounts: website log in / sign up / log out (cookie), and the same for the Android app (token).
public static class Accounts
{
    public const string AuthRateLimit = "auth";
    public const string AppPolicy = "app";
    private const string DatabaseDown = "FriendDex can't reach its database right now. Try again in a minute.";

    public record LoginRequest(string Email, string Password);
    public record RegisterRequest(string Email, string Password, string InviteCode);
    public record TokenResponse(string Token, string Email);

    public static void AddAccounts(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<AccountService>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<CurrentUser>();
        builder.Services.AddCascadingAuthenticationState();

        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(o =>
            {
                o.LoginPath = "/login";
                o.Cookie.Name = "frienddex_login";
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = SameSiteMode.Lax;
                o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                o.ExpireTimeSpan = TimeSpan.FromDays(30);
                o.SlidingExpiration = true;
            })
            .AddScheme<AuthenticationSchemeOptions, AppTokenAuthHandler>(AppTokenAuthHandler.SchemeName, null);

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(AppPolicy, p => p.AddAuthenticationSchemes(AppTokenAuthHandler.SchemeName).RequireAuthenticatedUser());

        // Hosts like Render sit in front of the app; use the visitor's real address for rate limits.
        builder.Services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            o.ForwardLimit = 1;
            o.KnownIPNetworks.Clear();
            o.KnownProxies.Clear();
        });

        // At most 10 login / sign-up attempts per minute from one address, to slow down password guessing.
        builder.Services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(AuthRateLimit, ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
        });
    }

    public static void MapAccounts(this WebApplication app)
    {
        // ---- Website (HTML forms + cookie) ----

        app.MapGet("/login", (string? error, string? email) => Html(LoginPage(error, email)));

        app.MapPost("/login", async (HttpContext http, AccountService accounts, ILogger<AccountService> log) =>
        {
            var form = await http.Request.ReadFormAsync();
            var email = form["email"].ToString();
            Domain.User? user;
            try
            {
                user = await accounts.CheckPasswordAsync(email, form["password"].ToString());
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                log.LogError(ex, "Login failed: database problem");
                return Results.Redirect($"/login?error=database&email={Uri.EscapeDataString(email)}");
            }
            if (user == null) return Results.Redirect($"/login?error=wrong&email={Uri.EscapeDataString(email)}");
            await SignInAsync(http, user.Id, user.Email);
            return Results.Redirect("/friends");
        }).DisableAntiforgery().RequireRateLimiting(AuthRateLimit);

        app.MapGet("/register", (string? error, string? email) => Html(RegisterPage(error, email)));

        app.MapPost("/register", async (HttpContext http, AccountService accounts, ILogger<AccountService> log) =>
        {
            var form = await http.Request.ReadFormAsync();
            var email = form["email"].ToString();
            try
            {
                var user = await accounts.RegisterAsync(email, form["password"].ToString(), form["invite"].ToString());
                await SignInAsync(http, user.Id, user.Email);
                return Results.Redirect("/friends");
            }
            catch (InvalidOperationException ex)
            {
                return Results.Redirect($"/register?error={Uri.EscapeDataString(ex.Message)}&email={Uri.EscapeDataString(email)}");
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Sign-up failed: database problem");
                return Results.Redirect($"/register?error={Uri.EscapeDataString(DatabaseDown)}&email={Uri.EscapeDataString(email)}");
            }
        }).DisableAntiforgery().RequireRateLimiting(AuthRateLimit);

        app.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/login");
        }).DisableAntiforgery();

        // ---- Android app (JSON + token) ----

        var auth = app.MapGroup("/api/auth").RequireRateLimiting(AuthRateLimit);

        auth.MapPost("/login", async (LoginRequest body, AccountService accounts, ILogger<AccountService> log) =>
        {
            Domain.User? user;
            try
            {
                user = await accounts.CheckPasswordAsync(body.Email, body.Password);
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                log.LogError(ex, "App login failed: database problem");
                return Results.Json(new FriendApi.ErrorResponse(DatabaseDown), statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            return user == null
                ? Results.Json(new FriendApi.ErrorResponse("Wrong email or password."), statusCode: StatusCodes.Status401Unauthorized)
                : Results.Ok(new TokenResponse(await accounts.CreateApiTokenAsync(user.Id), user.Email));
        });

        auth.MapPost("/register", async (RegisterRequest body, AccountService accounts, ILogger<AccountService> log) =>
        {
            try
            {
                var user = await accounts.RegisterAsync(body.Email, body.Password, body.InviteCode);
                return Results.Ok(new TokenResponse(await accounts.CreateApiTokenAsync(user.Id), user.Email));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new FriendApi.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                log.LogError(ex, "App sign-up failed: database problem");
                return Results.Json(new FriendApi.ErrorResponse(DatabaseDown), statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        auth.MapPost("/logout", async (HttpContext http, AccountService accounts) =>
        {
            var header = http.Request.Headers.Authorization.ToString();
            if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                await accounts.RevokeApiTokenAsync(header["Bearer ".Length..].Trim());
            }
            return Results.NoContent();
        });
    }

    private static Task SignInAsync(HttpContext http, int userId, string email)
    {
        var identity = new ClaimsIdentity(
            [new Claim(CurrentUser.UserIdClaim, userId.ToString()), new Claim(ClaimTypes.Name, email)],
            CookieAuthenticationDefaults.AuthenticationScheme);
        return http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
    }

    // ---- Simple server-rendered pages in the FriendDex style ----

    private static IResult Html(string body) => Results.Content(body, "text/html");

    private static string LoginPage(string? error, string? email) => Shell("Log in", $"""
        <form class="win" method="post" action="/login">
            <h1 class="win-title">Log in</h1>
            <div class="field">
                <label for="email">Email</label>
                <input id="email" name="email" type="email" class="px-input" autocomplete="username" required value="{Enc(email)}" />
            </div>
            <div class="field">
                <label for="password">Password</label>
                <input id="password" name="password" type="password" class="px-input" autocomplete="current-password" required />
            </div>
            {(error == "database" ? $"<p class=\"error-text\" role=\"alert\">{DatabaseDown}</p>"
                : error != null ? "<p class=\"error-text\" role=\"alert\">Wrong email or password.</p>" : "")}
            <div class="btn-row">
                <button type="submit" class="px-btn px-btn-leaf">Log in</button>
                <a class="text-btn" href="/register">Create an account</a>
            </div>
        </form>
        """);

    private static string RegisterPage(string? error, string? email) => Shell("Create an account", $"""
        <form class="win" method="post" action="/register">
            <h1 class="win-title">Create an account</h1>
            <div class="field">
                <label for="email">Email</label>
                <input id="email" name="email" type="email" class="px-input" autocomplete="username" required value="{Enc(email)}" />
            </div>
            <div class="field">
                <label for="password">Password (at least {AccountService.MinPasswordLength} characters)</label>
                <input id="password" name="password" type="password" class="px-input" autocomplete="new-password"
                       minlength="{AccountService.MinPasswordLength}" required />
            </div>
            <div class="field">
                <label for="invite">Invite code</label>
                <input id="invite" name="invite" class="px-input" autocomplete="off" required />
            </div>
            {(error != null ? $"<p class=\"error-text\" role=\"alert\">{Enc(error)}</p>" : "")}
            <div class="btn-row">
                <button type="submit" class="px-btn px-btn-leaf">Create account</button>
                <a class="text-btn" href="/login">I already have an account</a>
            </div>
        </form>
        """);

    private static string Shell(string title, string content) => $"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1.0" />
            <title>{title} · FriendDex</title>
            <link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=M+PLUS+Rounded+1c:wght@400;700&family=Pixelify+Sans:wght@400;600;700&display=swap" />
            <link rel="stylesheet" href="/_content/FriendDex.UI/app.css" />
        </head>
        <body>
            <main style="max-width: 28rem; margin: 3rem auto; padding: 0 1rem;">
                <p style="font-family: var(--font-pixel); font-weight: 700; font-size: 2rem; color: #fff; margin: 0 0 1rem;
                    text-shadow: 3px 0 #1f1b1c, -3px 0 #1f1b1c, 0 3px #1f1b1c, 0 -3px #1f1b1c, 0 6px #1f1b1c;">FriendDex</p>
                {content}
            </main>
        </body>
        </html>
        """;

    private static string Enc(string? s) => WebUtility.HtmlEncode(s ?? "");
}
