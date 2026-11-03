using System.Security.Cryptography;
using System.Text;
using SkiaSharp;

namespace NetIdenticon;

/// <summary>Renders GitHub-style identicons, pixel-identical to dgraham/identicon.</summary>
public static class Identicon
{
    internal const int Size = 420;
    internal const int CellSize = 70;
    internal const int Margin = CellSize / 2;
    internal const int Cells = 5;

    private static readonly SKColor Background = new(240, 240, 240);

    /// <summary>Renders a 420×420 bitmap. The caller disposes it.</summary>
    /// <param name="hash">At least 16 bytes (normally an MD5 digest).</param>
    public static SKBitmap Render(ReadOnlySpan<byte> hash)
    {
        ValidateHash(hash);

        (byte r, byte g, byte b) = Foreground(hash);
        bool[] cells = Grid(hash);

        SKBitmap bitmap = new(new SKImageInfo(Size, Size, SKColorType.Rgba8888, SKAlphaType.Opaque));
        try
        {
            using SKCanvas canvas = new(bitmap);
            canvas.Clear(Background);

            using SKPaint paint = new() { Color = new SKColor(r, g, b), IsAntialias = false, Style = SKPaintStyle.Fill };
            for (int row = 0; row < Cells; row++)
            {
                for (int col = 0; col < Cells; col++)
                {
                    if (!cells[row * Cells + col]) continue;
                    int x = col * CellSize + Margin;
                    int y = row * CellSize + Margin;
                    canvas.DrawRect(SKRect.Create(x, y, CellSize, CellSize), paint);
                }
            }
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    public static byte[] RenderPng(ReadOnlySpan<byte> hash)
    {
        using SKBitmap bitmap = Render(hash);
        using SKData data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>MD5 of the UTF-8 bytes of <paramref name="text"/>, then <see cref="RenderPng(ReadOnlySpan{byte})"/>.</summary>
    public static byte[] RenderPng(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return RenderPng(MD5.HashData(Encoding.UTF8.GetBytes(text)));
    }

    private static void ValidateHash(ReadOnlySpan<byte> hash)
    {
        if (hash.Length < 16)
            throw new ArgumentException("Hash must be at least 16 bytes.", nameof(hash));
    }

    // https://processing.org/reference/map_.html (dmin placement is a bug in the reference, ported as written)
    internal static float Map(uint value, uint vmin, uint vmax, uint dmin, uint dmax) =>
        (float)((value - vmin) * (dmax - dmin)) / (float)((vmax - vmin) + dmin);

    internal static (byte R, byte G, byte B) Foreground(ReadOnlySpan<byte> s)
    {
        uint h = (uint)(((s[12] & 0x0f) << 8) | s[13]);
        float hue = Map(h, 0, 4095, 0, 360);
        float sat = Map(s[14], 0, 255, 0, 20);
        float lum = Map(s[15], 0, 255, 0, 20);
        return Hsl.ToRgb(hue, 65.0f - sat, 75.0f - lum);
    }

    /// <summary>Row-major 5×5 grid (index = row * 5 + col), mirrored horizontally.</summary>
    internal static bool[] Grid(ReadOnlySpan<byte> hash)
    {
        byte[] nibbles = Nibbler.Split(hash);
        bool[] pixels = new bool[Cells * Cells];
        int next = 0;
        for (int col = 2; col >= 0; col--)
        {
            for (int row = 0; row < Cells; row++)
            {
                bool paint = next < nibbles.Length && nibbles[next++] % 2 == 0;
                pixels[col + row * Cells] = paint;
                pixels[(4 - col) + row * Cells] = paint;
            }
        }
        return pixels;
    }
}
