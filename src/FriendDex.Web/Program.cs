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
builder.Services.AddHttpClient<ChibiGenerator>(c => c.Timeout = TimeSpan.FromMinutes(2));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
// Serves a friend's AI-generated chibi image.
app.MapGet("/friends/{id:int}/avatar", async (int id, IDbContextFactory<AppDbContext> factory) =>
{
    await using var db = await factory.CreateDbContextAsync();
    var friend = await db.Friends.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id);
    return friend?.Avatar is { Length: > 0 } bytes
        ? Results.File(bytes, friend.AvatarContentType ?? "image/png")
        : Results.NotFound();
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
