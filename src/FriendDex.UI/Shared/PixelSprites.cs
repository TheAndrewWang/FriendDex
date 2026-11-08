using System.Text.RegularExpressions;
using FriendDex.Web.Domain;

namespace FriendDex.Web.Components.Shared;

public enum SpriteKind { Boy, Girl, Neutral }

// Default pixel sprites, drawn as character maps (one character = one pixel).
// Legend: . empty  K outline  H hair  S skin  R blush  M mouth  B band/pom  A beanie  C shirt  P pants
public static class PixelSprites
{
    public static readonly string[] Boy =
    [
        "....KKKKKKKK....",
        "...KHHHHHHHHK...",
        "..KHHHHHHHHHHK..",
        "..KBBBBBBBBBBK..",
        ".KHSSSSSSSSSSHK.",
        ".KSSSSSSSSSSSSK.",
        ".KSSKSSSSSSKSSK.",
        ".KSSKSSSSSSKSSK.",
        ".KSRSSSMMSSSRSK.",
        "..KSSSSSSSSSSK..",
        "...KKKKKKKKKK...",
        "..KCCCCCCCCCCK..",
        ".KSKCCCCCCCCKSK.",
        "..KKCCCCCCCCKK..",
        "...KPPPKKPPPK...",
        "...KKKK..KKKK...",
    ];

    public static readonly string[] Girl =
    [
        ".KK..KKKKKK..KK.",
        "KHHKKHHHHHHKKHHK",
        "KHHHHHHHHHHHHHHK",
        ".KKHHHHHHHHHHKK.",
        ".KHHSSSSSSSSHHK.",
        ".KHSSSSSSSSSSHK.",
        ".KSSKSSSSSSKSSK.",
        ".KSSKSSSSSSKSSK.",
        ".KSRSSSMMSSSRSK.",
        "..KSSSSSSSSSSK..",
        "...KKKKKKKKKK...",
        "..KCCCCCCCCCCK..",
        ".KSKCCCCCCCCKSK.",
        "..KCCCCCCCCCCK..",
        ".KCCCCCCCCCCCCK.",
        "..KKKSKKKKSKKK..",
        "....KK....KK....",
    ];

    public static readonly string[] Neutral =
    [
        ".......KK.......",
        "......KBBK......",
        "....KKKKKKKK....",
        "...KAAAAAAAAK...",
        "..KAAAAAAAAAAK..",
        "..KKKKKKKKKKKK..",
        ".KHSSSSSSSSSSHK.",
        ".KSSKSSSSSSKSSK.",
        ".KSSKSSSSSSKSSK.",
        ".KSRSSSMMSSSRSK.",
        "..KSSSSSSSSSSK..",
        "...KKKKKKKKKK...",
        "..KCCCCCCCCCCK..",
        ".KSKCCCCCCCCKSK.",
        "..KKCCCCCCCCKK..",
        "...KPPPKKPPPK...",
        "...KKKK..KKKK...",
    ];

    // Gender is free text, so match common words; anything else gets the neutral sprite.
    public static SpriteKind KindFor(string? gender)
    {
        if (string.IsNullOrWhiteSpace(gender)) return SpriteKind.Neutral;
        var g = gender.Trim().ToLowerInvariant();
        if (Regex.IsMatch(g, @"\b(she|her|hers|female|woman|girl|f)\b")) return SpriteKind.Girl;
        if (Regex.IsMatch(g, @"\b(he|him|his|male|man|boy|m)\b")) return SpriteKind.Boy;
        return SpriteKind.Neutral;
    }

    public static string[] MapFor(SpriteKind kind) => kind switch
    {
        SpriteKind.Boy => Boy,
        SpriteKind.Girl => Girl,
        _ => Neutral,
    };

    public static Dictionary<char, string> PaletteFor(SpriteKind kind, string? favoriteColor)
    {
        var shirt = SafeColor(favoriteColor) ?? kind switch
        {
            SpriteKind.Boy => "#e8546e",
            SpriteKind.Girl => "#5dbb4f",
            _ => "#74c3ea",
        };
        return new Dictionary<char, string>
        {
            ['K'] = "#1f1b1c",
            ['H'] = kind switch { SpriteKind.Boy => "#8a4b24", SpriteKind.Girl => "#2f2340", _ => "#c9733a" },
            ['S'] = "#ffd8b4",
            ['R'] = "#f59aa8",
            ['M'] = "#c0505a",
            ['B'] = "#ffc93c",
            ['A'] = "#8ed1f2",
            ['C'] = shirt,
            ['P'] = "#3d4f8a",
        };
    }

    // One horizontal run of same-colored pixels, so the SVG needs fewer rectangles.
    public record Run(int X, int Y, int Width, string Fill);

    public static List<Run> Runs(string[] map, Dictionary<char, string> palette)
    {
        var runs = new List<Run>();
        for (var y = 0; y < map.Length; y++)
        {
            var row = map[y];
            var x = 0;
            while (x < row.Length)
            {
                var ch = row[x];
                var start = x;
                while (x < row.Length && row[x] == ch) x++;
                if (palette.TryGetValue(ch, out var fill)) runs.Add(new Run(start, y, x - start, fill));
            }
        }
        return runs;
    }

    // Only plain color names ("teal") or hex codes ("#3a7"), so user text can't break the SVG.
    public static string? SafeColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color)) return null;
        var c = color.Trim().ToLowerInvariant().Replace(" ", "");
        return Regex.IsMatch(c, "^(#[0-9a-f]{3}|#[0-9a-f]{6}|[a-z]{3,20})$") ? c : null;
    }
}
