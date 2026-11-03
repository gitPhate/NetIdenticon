using SkiaSharp;
using Xunit;

namespace NetIdenticon.Tests;

internal static class PixelAssert
{
    public static void RgbEqual(SKBitmap expected, SKBitmap actual)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        for (int y = 0; y < expected.Height; y++)
        {
            for (int x = 0; x < expected.Width; x++)
            {
                SKColor e = expected.GetPixel(x, y);
                SKColor a = actual.GetPixel(x, y);
                if (e.Red != a.Red || e.Green != a.Green || e.Blue != a.Blue)
                    Assert.Fail($"Pixel ({x},{y}) differs: expected {e}, actual {a}");
            }
        }
    }
}
