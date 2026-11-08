using SkiaSharp;

namespace FriendDex.Web.Services;

// Snaps an image to a small pixel grid (like perler beads), so sprites always look chunky.
public static class Pixelator
{
    public const int Grid = 48;

    public static byte[] ToPixelGrid(byte[] imageBytes, int grid = Grid)
    {
        using var source = SKBitmap.Decode(imageBytes)
            ?? throw new InvalidOperationException("The generated image couldn't be read.");

        // Trim the white background and center the character in a square with a small margin,
        // so every sprite ends up the same size and position no matter how the model framed it.
        using var square = FrameCharacter(source);

        // Nearest-neighbor keeps hard edges and the original flat colors (no blur between cells).
        var info = new SKImageInfo(grid, grid, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var small = square.Resize(info, new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None))
            ?? throw new InvalidOperationException("The generated image couldn't be resized.");

        using var image = SKImage.FromBitmap(small);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }

    // Pixels lighter than this on every channel count as background.
    private const byte BackgroundThreshold = 235;

    private static SKBitmap FrameCharacter(SKBitmap source)
    {
        int minX = source.Width, minY = source.Height, maxX = -1, maxY = -1;
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var c = source.GetPixel(x, y);
                var isBackground = c.Alpha < 16 ||
                    (c.Red >= BackgroundThreshold && c.Green >= BackgroundThreshold && c.Blue >= BackgroundThreshold);
                if (isBackground) continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        // Nothing but background: fall back to the whole image.
        if (maxX < 0) { minX = 0; minY = 0; maxX = source.Width - 1; maxY = source.Height - 1; }

        var width = maxX - minX + 1;
        var height = maxY - minY + 1;
        var side = (int)Math.Ceiling(Math.Max(width, height) * 1.1);   // ~5% margin on each side

        var square = new SKBitmap(side, side);
        using var canvas = new SKCanvas(square);
        canvas.Clear(SKColors.White);
        var dest = SKRect.Create((side - width) / 2f, (side - height) / 2f, width, height);
        using var sourceImage = SKImage.FromBitmap(source);
        canvas.DrawImage(sourceImage, new SKRect(minX, minY, maxX + 1, maxY + 1), dest, new SKSamplingOptions(SKFilterMode.Nearest));
        return square;
    }
}
