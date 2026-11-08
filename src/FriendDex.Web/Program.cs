using FriendDex.Web.Api;
using FriendDex.Web.Components;
using FriendDex.Web.Data;
using FriendDex.Web.Services;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using MongoDB.EntityFrameworkCore.Extensions;

// Load API keys from .env before configuration is read.
DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// MongoDB Atlas: connection string and database name come from .env (MONGODB_URI, MONGODB_DATABASE).
var mongoUri = builder.Configuration["MONGODB_URI"];
if (string.IsNullOrWhiteSpace(mongoUri))
{
    throw new InvalidOperationException(
        "MONGODB_URI isn't set. Add your MongoDB Atlas connection string to .env, then restart the app.");
}
var mongoDatabase = builder.Configuration["MONGODB_DATABASE"] is { Length: > 0 } name ? name : "frienddex";

// Give up on an unreachable database after 10 seconds (the driver's default is 30).
var mongoSettings = MongoClientSettings.FromConnectionString(mongoUri);
mongoSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(10);
builder.Services.AddSingleton<IMongoClient>(new MongoClient(mongoSettings));
builder.Services.AddDbContextFactory<AppDbContext>((services, o) =>
    o.UseMongoDB(services.GetRequiredService<IMongoClient>(), mongoDatabase));

builder.Services.AddSingleton<FriendNoteAgent>();
builder.Services.AddScoped<IFriendDexService, ServerFriendDexService>();
builder.AddAccounts();
builder.Services.AddHttpClient<ChibiGenerator>(c => c.Timeout = TimeSpan.FromMinutes(2));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseForwardedHeaders();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
// Quick check that the server can reach MongoDB. Shows no secrets, only whether it works.
app.MapGet("/health", async (IMongoClient mongo, ILogger<Program> log) =>
{
    try
    {
        await mongo.GetDatabase("admin").RunCommandAsync<MongoDB.Bson.BsonDocument>(new MongoDB.Bson.BsonDocument("ping", 1));
        return Results.Ok(new { server = "ok", database = "ok" });
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Health check: can't reach MongoDB");
        return Results.Json(new
        {
            server = "ok",
            database = "unreachable",
            hint = "Check MONGODB_URI and that MongoDB Atlas Network Access allows this server (0.0.0.0/0).",
            error = ex.GetType().Name,
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

// Log in / sign up (website and app), and the friend API for the Android app.
app.MapAccounts();
app.MapFriendApi();

// Every page needs a signed-in account; logged-out visitors go to /login.
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(FriendDex.Web.Services.IFriendDexService).Assembly)
    .RequireAuthorization();

app.Run();

// Lets the in-memory test server find this app.
public partial class Program;
