using FriendDex.Web.Domain;
using FriendDex.Web.Services;

namespace FriendDex.Tests;

public class ChibiGeneratorTests
{
    private static readonly string Png = Convert.ToBase64String([0x89, 0x50, 0x4E, 0x47]);

    [Fact]
    public void ExtractImage_FromOutputImage()
    {
        var img = ChibiGenerator.ExtractImage($$$"""{"id":"x","output_image":{"data":"{{{Png}}}","mime_type":"image/png"}}""");
        Assert.NotNull(img);
        Assert.Equal(4, img.Bytes.Length);
        Assert.Equal("image/png", img.ContentType);
    }

    [Fact]
    public void ExtractImage_FromStepsContentBlock()
    {
        var json = $$$"""
            {"steps":[{"type":"thought"},
                      {"type":"model_output","content":[{"type":"text","text":"Here you go"},
                                                        {"type":"image","mime_type":"image/jpeg","data":"{{{Png}}}"}]}]}
            """;
        var img = ChibiGenerator.ExtractImage(json);
        Assert.NotNull(img);
        Assert.Equal("image/jpeg", img.ContentType);
    }

    [Fact]
    public void ExtractImage_FromInlineData()
    {
        var json = $$$"""{"candidates":[{"content":{"parts":[{"inlineData":{"mimeType":"image/png","data":"{{{Png}}}"}}]}}]}""";
        Assert.NotNull(ChibiGenerator.ExtractImage(json));
    }

    [Fact]
    public void ExtractImage_NoImageOrBadJson_ReturnsNull()
    {
        Assert.Null(ChibiGenerator.ExtractImage("""{"steps":[{"type":"model_output","content":[{"type":"text","text":"no"}]}]}"""));
        Assert.Null(ChibiGenerator.ExtractImage("not json"));
        Assert.Null(ChibiGenerator.ExtractImage("""{"output_image":{"data":"%%%not-base64%%%"}}"""));
    }

    [Fact]
    public void BuildPrompt_IncludesColorLikesAndPhotoInstruction()
    {
        var friend = new Friend
        {
            Name = "Sam",
            FavoriteColor = "teal",
            Photo = [1, 2, 3],
            PhotoContentType = "image/png",
            Interests =
            [
                new Interest { Label = "bouldering", Kind = InterestKind.Like },
                new Interest { Label = "crowds", Kind = InterestKind.Dislike },
            ],
        };

        var prompt = ChibiGenerator.BuildPrompt(friend);

        Assert.Contains("chibi", prompt);
        Assert.Contains("pixel art sprite", prompt);
        Assert.Contains("teal", prompt);
        Assert.Contains("bouldering", prompt);
        Assert.DoesNotContain("crowds", prompt);
        Assert.Contains("attached photo", prompt);
    }
}

public class PixelatorTests
{
    [Fact]
    public void ToPixelGrid_MakesSmallSquarePngWithHardColors()
    {
        // 100x60 image: left half red, right half blue.
        using var bmp = new SkiaSharp.SKBitmap(100, 60);
        using (var canvas = new SkiaSharp.SKCanvas(bmp))
        {
            canvas.Clear(SkiaSharp.SKColors.Red);
            using var paint = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.Blue };
            canvas.DrawRect(50, 0, 50, 60, paint);
        }
        using var img = SkiaSharp.SKImage.FromBitmap(bmp);
        var input = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100).ToArray();

        var output = Pixelator.ToPixelGrid(input, grid: 24);

        using var result = SkiaSharp.SKBitmap.Decode(output);
        Assert.Equal(24, result.Width);
        Assert.Equal(24, result.Height);
        Assert.Equal(SkiaSharp.SKColors.Red, result.GetPixel(2, 12));    // no blended colors
        Assert.Equal(SkiaSharp.SKColors.Blue, result.GetPixel(21, 12));
    }
}
