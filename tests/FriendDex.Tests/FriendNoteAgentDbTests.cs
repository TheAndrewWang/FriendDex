using FriendDex.Web.Data;
using FriendDex.Web.Domain;
using FriendDex.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FriendDex.Tests;

public class FriendNoteAgentDbTests
{
    [Fact]
    public async Task ApplyThenSave_PersistsAddFlipAndRemove()
    {
        // Runs the real AppDbContext model on in-memory SQLite (no MongoDB needed for tests).
        // In-memory SQLite lives only while this connection stays open.
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;

        int friendId, sushiId, gymsId;
        await using (var db = new AppDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            var sam = new Friend
            {
                Name = "Sam",
                Interests =
                [
                    new Interest { Label = "sushi", Kind = InterestKind.Like },
                    new Interest { Label = "gyms", Kind = InterestKind.Like },
                ],
            };
            db.Friends.Add(sam);
            await db.SaveChangesAsync();
            friendId = sam.Id;
            sushiId = sam.Interests.Single(i => i.Label == "sushi").Id;
            gymsId = sam.Interests.Single(i => i.Label == "gyms").Id;
        }

        // Same steps the detail page takes when you click "Apply changes".
        await using (var db = new AppDbContext(options))
        {
            var tracked = await db.Friends.SingleAsync(f => f.Id == friendId);
            FriendNoteAgent.Apply(tracked, new FriendUpdateProposal("x", null, null, null,
                [new ProposedNewInterest("bouldering", "since spring", "Like")],
                [new ProposedInterestEdit(gymsId, "hates crowded gyms", "Dislike")],
                [sushiId]));
            await db.SaveChangesAsync();
        }

        await using (var db = new AppDbContext(options))
        {
            var interests = (await db.Friends.SingleAsync(f => f.Id == friendId)).Interests;

            Assert.DoesNotContain(interests, i => i.Id == sushiId);          // row really deleted
            Assert.Equal(InterestKind.Dislike, interests.Single(i => i.Id == gymsId).Kind);
            Assert.Equal("since spring", interests.Single(i => i.Label == "bouldering").Notes);
            Assert.Equal(2, interests.Count);
        }
    }
}

public class RandomIdTests
{
    [Fact]
    public async Task NewFriendsAndInterests_GetPositiveUniqueIds()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;

        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var a = new Friend { Name = "A", Interests = [new Interest { Label = "x" }, new Interest { Label = "y" }] };
        var b = new Friend { Name = "B" };
        db.Friends.AddRange(a, b);
        await db.SaveChangesAsync();

        Assert.True(a.Id > 0 && b.Id > 0);
        Assert.NotEqual(a.Id, b.Id);
        Assert.All(a.Interests, i => Assert.True(i.Id > 0));
        Assert.NotEqual(a.Interests[0].Id, a.Interests[1].Id);
    }
}
