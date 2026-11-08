using SkiaSharp;

namespace FriendDex.Web.Services;

// Snaps an image to a small pixel grid (like perler beads), so sprites always look chunky.
public static class Pixelator
{
    public const int Grid = 24;

    public static byte[] ToPixelGrid(byte[] imageBytes, int grid = Grid)
    {
        using var source = SKBitmap.Decode(imageBytes)
            ?? throw new InvalidOperationException("The generated image couldn't be read.");

        // Crop to a centered square so the grid cells stay square.
        var side = Math.Min(source.Width, source.Height);
        var crop = new SKRectI((source.Width - side) / 2, (source.Height - side) / 2,
                               (source.Width + side) / 2, (source.Height + side) / 2);
        using var square = new SKBitmap(side, side);
        source.ExtractSubset(square, crop);

        // Nearest-neighbor keeps hard edges and the original flat colors (no blur between cells).
        var info = new SKImageInfo(grid, grid, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var small = square.Resize(info, new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None))
            ?? throw new InvalidOperationException("The generated image couldn't be resized.");

        using var image = SKImage.FromBitmap(small);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }
}
