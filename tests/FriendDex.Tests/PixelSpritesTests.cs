using FriendDex.Web.Components.Shared;

namespace FriendDex.Tests;

public class PixelSpritesTests
{
    public static TheoryData<string> Maps => ["Boy", "Girl", "Neutral"];

    [Theory]
    [MemberData(nameof(Maps))]
    public void Maps_AreRectangular_AndUseOnlyKnownColors(string name)
    {
        var kind = Enum.Parse<SpriteKind>(name);
        var map = PixelSprites.MapFor(kind);
        var palette = PixelSprites.PaletteFor(kind, null);

        Assert.All(map, row => Assert.Equal(map[0].Length, row.Length));
        Assert.All(map.SelectMany(r => r), ch => Assert.True(ch == '.' || palette.ContainsKey(ch), $"Unknown pixel '{ch}'"));
    }

    [Theory]
    [InlineData("she/her", SpriteKind.Girl)]
    [InlineData("Female", SpriteKind.Girl)]
    [InlineData("woman", SpriteKind.Girl)]
    [InlineData("he/him", SpriteKind.Boy)]
    [InlineData("Male", SpriteKind.Boy)]
    [InlineData("they/them", SpriteKind.Neutral)]
    [InlineData("nonbinary", SpriteKind.Neutral)]
    [InlineData("", SpriteKind.Neutral)]
    [InlineData(null, SpriteKind.Neutral)]
    public void KindFor_MatchesGenderText(string? gender, SpriteKind expected)
    {
        Assert.Equal(expected, PixelSprites.KindFor(gender));
    }

    [Fact]
    public void Palette_UsesSafeFavoriteColorForShirt()
    {
        Assert.Equal("teal", PixelSprites.PaletteFor(SpriteKind.Boy, "Teal")['C']);
        Assert.Equal("#e8546e", PixelSprites.PaletteFor(SpriteKind.Boy, "red\" onload=\"x")['C']);
    }

    [Fact]
    public void Runs_MergeSameColoredPixels()
    {
        var runs = PixelSprites.Runs(["KKS.", "...."], new() { ['K'] = "k", ['S'] = "s" });
        Assert.Equal([new PixelSprites.Run(0, 0, 2, "k"), new PixelSprites.Run(2, 0, 1, "s")], runs);
    }
}
