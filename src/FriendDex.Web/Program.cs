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

builder.Services.AddSingleton<IMongoClient>(new MongoClient(mongoUri));
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
