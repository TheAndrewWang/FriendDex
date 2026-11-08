using FriendDex.Web.Domain;
using FriendDex.Web.Services;

namespace FriendDex.Tests;

public class FriendNoteAgentTests
{
    private static Friend Sam() => new()
    {
        Name = "Sam",
        Interests =
        [
            new Interest { Id = 1, Label = "sushi", Kind = InterestKind.Like },
            new Interest { Id = 2, Label = "gyms", Notes = "goes daily", Kind = InterestKind.Like },
        ],
    };

    [Fact]
    public void Parse_ReadsAllFields()
    {
        var json = """
            {"summary":"Added bouldering.","gender":null,"birthday":"1999-03-04","favorite_color":"teal",
             "add_interests":[{"label":"bouldering","notes":"since spring","kind":"Like"}],
             "update_interests":[{"id":2,"notes":"hates crowded gyms","kind":"Dislike"}],
             "remove_interest_ids":[1]}
            """;

        var p = FriendNoteAgent.Parse(json);

        Assert.Equal("Added bouldering.", p.Summary);
        Assert.Null(p.Gender);
        Assert.Equal("1999-03-04", p.Birthday);
        Assert.Equal("teal", p.FavoriteColor);
        Assert.Single(p.AddInterests);
        Assert.Equal(2, p.UpdateInterests[0].Id);
        Assert.Equal([1], p.RemoveInterestIds);
        Assert.False(p.IsEmpty);
    }

    [Fact]
    public void Parse_BlankStringsAndMissingListsBecomeEmpty()
    {
        var p = FriendNoteAgent.Parse("""{"summary":"Nothing.","gender":"  ","birthday":null,"favorite_color":""}""");

        Assert.Null(p.Gender);
        Assert.Null(p.FavoriteColor);
        Assert.Empty(p.AddInterests);
        Assert.True(p.IsEmpty);
    }

    [Fact]
    public void Apply_AddsUpdatesFlipsAndRemoves()
    {
        var sam = Sam();
        var p = new FriendUpdateProposal("x", "nonbinary", "1999-03-04", "teal",
            [new ProposedNewInterest("bouldering", "since spring", "Like")],
            [new ProposedInterestEdit(2, "hates crowded gyms", "Dislike")],
            [1]);

        FriendNoteAgent.Apply(sam, p);

        Assert.Equal("nonbinary", sam.Gender);
        Assert.Equal(new DateOnly(1999, 3, 4), sam.Birthday);
        Assert.Equal("teal", sam.FavoriteColor);
        Assert.DoesNotContain(sam.Interests, i => i.Label == "sushi");
        var gyms = Assert.Single(sam.Interests, i => i.Label == "gyms");
        Assert.Equal(InterestKind.Dislike, gyms.Kind);
        Assert.Equal("hates crowded gyms", gyms.Notes);
        var bouldering = Assert.Single(sam.Interests, i => i.Label == "bouldering");
        Assert.Equal(InterestKind.Like, bouldering.Kind);
    }

    [Fact]
    public void Apply_IgnoresInvalidValues()
    {
        var sam = Sam();
        var p = new FriendUpdateProposal("x", null, "March 4th", null,
            [new ProposedNewInterest("  ", null, "Like"), new ProposedNewInterest("tea", null, "Meh")],
            [new ProposedInterestEdit(99, "ghost", "Like")],
            [42]);

        FriendNoteAgent.Apply(sam, p);

        Assert.Null(sam.Birthday);                       // unparseable date ignored
        Assert.Equal(3, sam.Interests.Count);            // blank label skipped, unknown ids ignored
        Assert.Equal(InterestKind.Like, sam.Interests.Single(i => i.Label == "tea").Kind); // bad kind -> Like
    }

    [Fact]
    public void Apply_TruncatesLongLabels()
    {
        var sam = Sam();
        var p = new FriendUpdateProposal("x", null, null, null,
            [new ProposedNewInterest(new string('a', 80), null, "Like")], [], []);

        FriendNoteAgent.Apply(sam, p);

        Assert.Equal(50, sam.Interests.Last().Label.Length);
    }
}
