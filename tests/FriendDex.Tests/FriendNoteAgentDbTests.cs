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
            var tracked = await db.Friends.Include(f => f.Interests).SingleAsync(f => f.Id == friendId);
            FriendNoteAgent.Apply(tracked, new FriendUpdateProposal("x", null, null, null,
                [new ProposedNewInterest("bouldering", "since spring", "Like")],
                [new ProposedInterestEdit(gymsId, "hates crowded gyms", "Dislike")],
                [sushiId]));
            await db.SaveChangesAsync();
        }

        await using (var db = new AppDbContext(options))
        {
            var interests = await db.Interests.Where(i => i.FriendId == friendId).ToListAsync();

            Assert.DoesNotContain(interests, i => i.Id == sushiId);          // row really deleted
            Assert.Equal(InterestKind.Dislike, interests.Single(i => i.Id == gymsId).Kind);
            Assert.Equal("since spring", interests.Single(i => i.Label == "bouldering").Notes);
            Assert.Equal(2, interests.Count);
        }
    }
}
