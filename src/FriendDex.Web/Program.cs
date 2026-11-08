using FriendDex.Web.Components;
using FriendDex.Web.Data;
using FriendDex.Web.Services;
using Microsoft.EntityFrameworkCore;

// Load API keys from .env before configuration is read.
DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContextFactory<AppDbContext>(o => 
        o.UseSqlite(builder.Configuration.GetConnectionString("Default")));

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
    var avatar = await db.Friends.Where(f => f.Id == id)
        .Select(f => new { f.Avatar, f.AvatarContentType })
        .FirstOrDefaultAsync();
    return avatar?.Avatar is { Length: > 0 } bytes
        ? Results.File(bytes, avatar.AvatarContentType ?? "image/png")
        : Results.NotFound();
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
