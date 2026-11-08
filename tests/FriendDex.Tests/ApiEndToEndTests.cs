using System.Net;
using FriendDex.Web.Data;
using FriendDex.Web.Domain;
using FriendDex.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FriendDex.Tests;

// Runs the real server (accounts, API, ServerFriendDexService) in memory with SQLite instead of MongoDB,
// and talks to it the same way the Android app does (HttpAccountClient + HttpFriendDexService).
public class ApiEndToEndTests : IDisposable
{
    private const string Invite = "join-frienddex";
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly WebApplicationFactory<Program> factory;

    public ApiEndToEndTests()
    {
        // Program.cs refuses to start without a MongoDB address; the client never connects in these tests.
        Environment.SetEnvironmentVariable("MONGODB_URI", "mongodb://127.0.0.1:1");
        connection.Open();

        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
        {
            web.UseSetting("FRIENDDEX_INVITE_CODE", Invite);
            web.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDbContextFactory<AppDbContext>>();
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
                services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite(connection));
            });
        });

        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext().Database.EnsureCreated();
    }

    public void Dispose()
    {
        factory.Dispose();
        connection.Dispose();
    }

    private record FixedConnection(string Url, string Tok) : IServerConnection
    {
        public Task<(string ServerUrl, string Token)?> GetAsync() => Task.FromResult<(string, string)?>((Url, Tok));
    }

    private string BaseUrl => factory.Server.BaseAddress.ToString();

    // Signs up through the app's account client and returns that person's app data service.
    private async Task<HttpFriendDexService> SignUpAsync(string email)
    {
        var accounts = new HttpAccountClient(factory.CreateClient());
        var login = await accounts.RegisterAsync(BaseUrl, email, "correct-horse-battery", Invite);
        return new HttpFriendDexService(factory.CreateClient(), new FixedConnection(BaseUrl, login.Token));
    }

    [Fact]
    public async Task FullFlow_ThroughTheAppService()
    {
        var app = await SignUpAsync("sam.owner@example.com");

        var id = await app.SaveFriendAsync(new Friend { Name = "Sam", Gender = "she/her", Birthday = new DateOnly(1999, 3, 4), FavoriteColor = "teal" });
        await app.AddInterestAsync(id, new Interest { Label = "bouldering", Notes = "since spring", Kind = InterestKind.Like });
        await app.AddInterestAsync(id, new Interest { Label = "crowds", Kind = InterestKind.Dislike });
        await app.SetPhotoAsync(id, [0x89, 0x50, 0x4E, 0x47], "image/png");

        var sam = await app.GetFriendAsync(id);
        Assert.NotNull(sam);
        Assert.Equal(new DateOnly(1999, 3, 4), sam.Birthday);
        Assert.Equal(2, sam.Interests.Count);
        Assert.True(sam.HasPhoto);
        Assert.Null(sam.Photo);                       // photo bytes never leave the server

        var crowds = sam.Interests.Single(i => i.Label == "crowds");
        await app.UpdateInterestAsync(id, new Interest { Id = crowds.Id, Label = "crowds", Notes = "loud ones", Kind = InterestKind.Like });
        sam.Name = "Samantha";
        await app.SaveFriendAsync(sam);               // must not wipe the photo or interests

        var bouldering = sam.Interests.Single(i => i.Label == "bouldering");
        await app.ApplyProposalAsync(id, new FriendUpdateProposal("x", null, null, "blue",
            [new ProposedNewInterest("coffee", null, "Like")], [], [bouldering.Id]));

        sam = (await app.GetFriendAsync(id))!;
        Assert.Equal("Samantha", sam.Name);
        Assert.Equal("blue", sam.FavoriteColor);
        Assert.True(sam.HasPhoto);
        Assert.Equal(["coffee", "crowds"], sam.Interests.Select(i => i.Label).OrderBy(l => l));
        Assert.Equal("loud ones", sam.Interests.Single(i => i.Label == "crowds").Notes);

        await app.DeleteFriendAsync(id);
        Assert.Null(await app.GetFriendAsync(id));
        Assert.Empty(await app.GetFriendsAsync());
    }

    [Fact]
    public async Task EachAccount_OnlySeesItsOwnFriends()
    {
        var alice = await SignUpAsync("alice@example.com");
        var bob = await SignUpAsync("bob@example.com");

        var aliceFriend = await alice.SaveFriendAsync(new Friend { Name = "Alice's friend" });
        await bob.SaveFriendAsync(new Friend { Name = "Bob's friend" });

        Assert.Equal(["Alice's friend"], (await alice.GetFriendsAsync()).Select(f => f.Name));
        Assert.Equal(["Bob's friend"], (await bob.GetFriendsAsync()).Select(f => f.Name));

        // Bob knows Alice's friend's ID but can't read, change or delete it.
        Assert.Null(await bob.GetFriendAsync(aliceFriend));
        await Assert.ThrowsAsync<InvalidOperationException>(() => bob.AddInterestAsync(aliceFriend, new Interest { Label = "x" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            bob.SaveFriendAsync(new Friend { Id = aliceFriend, Name = "hijacked" }));
        await bob.DeleteFriendAsync(aliceFriend);     // silently does nothing

        var stillThere = await alice.GetFriendAsync(aliceFriend);
        Assert.Equal("Alice's friend", stillThere!.Name);
        Assert.Empty(stillThere.Interests);
    }

    [Fact]
    public async Task SignUp_NeedsTheInviteCode_AndUniqueEmail()
    {
        var accounts = new HttpAccountClient(factory.CreateClient());

        var badCode = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            accounts.RegisterAsync(BaseUrl, "new@example.com", "correct-horse-battery", "guess"));
        Assert.Contains("invite code", badCode.Message);

        var shortPassword = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            accounts.RegisterAsync(BaseUrl, "new@example.com", "short", Invite));
        Assert.Contains("8 characters", shortPassword.Message);

        await accounts.RegisterAsync(BaseUrl, "new@example.com", "correct-horse-battery", Invite);
        var duplicate = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            accounts.RegisterAsync(BaseUrl, "NEW@example.com", "correct-horse-battery", Invite));
        Assert.Contains("already exists", duplicate.Message);
    }

    [Fact]
    public async Task Login_ChecksPassword_AndLogoutRevokesTheToken()
    {
        var accounts = new HttpAccountClient(factory.CreateClient());
        await accounts.RegisterAsync(BaseUrl, "kai@example.com", "correct-horse-battery", Invite);

        await Assert.ThrowsAsync<InvalidOperationException>(() => accounts.LoginAsync(BaseUrl, "kai@example.com", "wrong-password"));

        var login = await accounts.LoginAsync(BaseUrl, " Kai@Example.com ", "correct-horse-battery");
        var app = new HttpFriendDexService(factory.CreateClient(), new FixedConnection(BaseUrl, login.Token));
        Assert.Empty(await app.GetFriendsAsync());

        await accounts.LogoutAsync(BaseUrl, login.Token);
        var expired = await Assert.ThrowsAsync<InvalidOperationException>(() => app.GetFriendsAsync());
        Assert.Contains("Log in again", expired.Message);
    }

    [Fact]
    public async Task Api_WithoutLogin_IsRejected()
    {
        var raw = await factory.CreateClient().GetAsync("/api/friends");
        Assert.Equal(HttpStatusCode.Unauthorized, raw.StatusCode);

        var forged = new HttpFriendDexService(factory.CreateClient(), new FixedConnection(BaseUrl, "made-up-token"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => forged.GetFriendsAsync());
    }

    [Fact]
    public async Task WebsitePages_NeedLogin_AndShowOnlyYourFriends()
    {
        var other = await SignUpAsync("other@example.com");
        await other.SaveFriendAsync(new Friend { Name = "Not yours" });

        var browser = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var loggedOut = await browser.GetAsync("/friends");
        Assert.Equal(HttpStatusCode.Redirect, loggedOut.StatusCode);
        Assert.StartsWith("/login", loggedOut.Headers.Location!.PathAndQuery);

        var signUp = await browser.PostAsync("/register", new FormUrlEncodedContent(
        [
            new("email", "rin@example.com"), new("password", "correct-horse-battery"), new("invite", Invite),
        ]));
        Assert.Equal("/friends", signUp.Headers.Location!.ToString());
        var cookie = signUp.Headers.GetValues("Set-Cookie").First().Split(';')[0];

        // Add a friend through the app API as the same person, then view the website.
        var login = await new HttpAccountClient(factory.CreateClient()).LoginAsync(BaseUrl, "rin@example.com", "correct-horse-battery");
        var rin = new HttpFriendDexService(factory.CreateClient(), new FixedConnection(BaseUrl, login.Token));
        var id = await rin.SaveFriendAsync(new Friend { Name = "Jun", Gender = "he/him" });

        async Task<string> Page(string path)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("Cookie", cookie);
            var response = await browser.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await response.Content.ReadAsStringAsync();
        }

        var list = await Page("/friends");
        Assert.Contains("Jun", list);
        Assert.DoesNotContain("Not yours", list);
        Assert.Contains("rin@example.com", list);
        Assert.Contains("Friend passport", await Page($"/friends/{id}"));

        var wrongLogin = await browser.PostAsync("/login", new FormUrlEncodedContent(
            [new("email", "rin@example.com"), new("password", "nope")]));
        Assert.Contains("error=", wrongLogin.Headers.Location!.ToString());
    }

    [Fact]
    public async Task FirstAccount_GetsFriendsCreatedBeforeAccountsExisted()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
            db.Friends.Add(new Friend { Name = "From before accounts" });   // OwnerId = 0
            await db.SaveChangesAsync();
        }

        var first = await SignUpAsync("owner@example.com");
        var second = await SignUpAsync("guest@example.com");

        Assert.Equal(["From before accounts"], (await first.GetFriendsAsync()).Select(f => f.Name));
        Assert.Empty(await second.GetFriendsAsync());
    }
}
