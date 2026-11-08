using FriendDex.Web.Data;
using FriendDex.Web.Domain;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using MongoDB.EntityFrameworkCore.Extensions;

namespace FriendDex.Tests;

public class MongoModelTests
{
    // Builds the real model with the MongoDB provider. The client never connects
    // (nothing is queried), but the provider still validates every mapping.
    [Fact]
    public void Model_IsValidForMongoDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMongoDB(new MongoClient("mongodb://127.0.0.1:1"), "frienddex-test")
            .Options;
        using var db = new AppDbContext(options);

        var friend = db.Model.FindEntityType(typeof(Friend))!;
        var interest = db.Model.FindEntityType(typeof(Interest))!;

        Assert.Equal("friends", friend.GetCollectionName());
        Assert.True(interest.IsOwned());          // embedded in the friend document
        Assert.NotNull(friend.FindProperty(nameof(Friend.Birthday)));
    }
}
