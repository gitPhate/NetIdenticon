using Xunit;

namespace NetIdenticon.Tests;

public class NibblerTests
{
    [Fact]
    public void SplitsHighNibbleFirst()
    {
        Assert.Equal(new byte[] { 0x2, 0xa, 0xf, 0x0 }, Nibbler.Split(new byte[] { 0x2a, 0xf0 }));
    }

    [Fact]
    public void EmptyInputYieldsNoNibbles() => Assert.Empty(Nibbler.Split(ReadOnlySpan<byte>.Empty));
}

public class HslTests
{
    [Theory]
    [InlineData(0f, 0f, 0f, 0, 0, 0)]
    [InlineData(0f, 0f, 100f, 255, 255, 255)]
    [InlineData(0f, 100f, 50f, 255, 0, 0)]
    [InlineData(120f, 100f, 50f, 0, 255, 0)]
    [InlineData(240f, 100f, 50f, 0, 0, 255)]
    public void Converts(float h, float s, float l, int r, int g, int b)
    {
        Assert.Equal(((byte)r, (byte)g, (byte)b), Hsl.ToRgb(h, s, l));
    }
}

public class GridTests
{
    [Fact]
    public void AllEvenNibblesFillEverything()
    {
        Assert.All(Identicon.Grid(new byte[16]), Assert.True);
    }

    [Fact]
    public void AllOddNibblesFillNothing()
    {
        byte[] hash = new byte[16];
        Array.Fill(hash, (byte)0x11);
        Assert.All(Identicon.Grid(hash), Assert.False);
    }

    [Fact]
    public void ColumnsConsumedDescendingAndMirrored()
    {
        // nibbles 0..4 -> col 2, 5..9 -> col 1, 10..14 -> col 0 ; even = filled.
        // Make only the first nibble (col 2, row 0) even; the rest odd.
        byte[] hash = new byte[16];
        Array.Fill(hash, (byte)0x11);
        hash[0] = 0x01; // nibbles: 0 (even), 1 ...
        bool[] grid = Identicon.Grid(hash);
        Assert.True(grid[2]);                 // (col 2, row 0)
        Assert.Equal(1, grid.Count(c => c));  // mirror of col 2 is col 2
    }

    [Fact]
    public void OuterColumnIsMirrored()
    {
        // 11th nibble (index 10) is col 0, row 0. Make it even, others odd.
        byte[] hash = new byte[16];
        Array.Fill(hash, (byte)0x11);
        hash[5] = 0x01; // nibbles 10 (0x0), 11 (0x1)
        bool[] grid = Identicon.Grid(hash);
        Assert.True(grid[0]);
        Assert.True(grid[4]);
        Assert.Equal(2, grid.Count(c => c));
    }

    [Fact]
    public void OnlyFirst15NibblesUsed()
    {
        byte[] a = new byte[16];
        byte[] b = new byte[16];
        b[12] = 0xff; b[13] = 0xff; b[14] = 0xff; b[15] = 0xff;
        Assert.Equal(Identicon.Grid(a), Identicon.Grid(b));
    }
}

public class IdenticonTests
{
    private static byte[] Hash(int seed)
    {
        byte[] h = new byte[16];
        new Random(seed).NextBytes(h);
        return h;
    }

    [Fact]
    public void ShortHashThrows()
    {
        Assert.Throws<ArgumentException>(() => Identicon.Render(new byte[15]));
        Assert.Throws<ArgumentException>(() => Identicon.RenderPng(new byte[15]));
    }

    [Fact]
    public void RenderHasExpectedSize()
    {
        using SkiaSharp.SKBitmap bmp = Identicon.Render(Hash(1));
        Assert.Equal(420, bmp.Width);
        Assert.Equal(420, bmp.Height);
        Assert.Equal(new SkiaSharp.SKColor(240, 240, 240), bmp.GetPixel(0, 0));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void PngRoundTripsToSamePixels(int seed)
    {
        byte[] hash = Hash(seed);
        using SkiaSharp.SKBitmap expected = Identicon.Render(hash);
        using SkiaSharp.SKBitmap decoded = SkiaSharp.SKBitmap.Decode(Identicon.RenderPng(hash));
        PixelAssert.RgbEqual(expected, decoded);
    }

    [Fact]
    public void TextOverloadHashesWithMd5()
    {
        byte[] md5 = System.Security.Cryptography.MD5.HashData("foo"u8);
        Assert.Equal(Identicon.RenderPng(md5), Identicon.RenderPng("foo"));
    }
}

public class GoldenTests
{
    // Golden PNGs live in tests/NetIdenticon.Tests/golden/<input>.png, produced with
    // `printf <input> | identicon > <input>.png` from the Rust reference.
    [Fact]
    public void MatchesRustReference()
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "golden");
        string[] files = Directory.Exists(dir) ? Directory.GetFiles(dir, "*.png") : [];
        Assert.NotEmpty(files);
        foreach (string file in files)
        {
            string input = Path.GetFileNameWithoutExtension(file);
            byte[] hash = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(input));
            using SkiaSharp.SKBitmap expected = SkiaSharp.SKBitmap.Decode(file);
            using SkiaSharp.SKBitmap actual = Identicon.Render(hash);
            PixelAssert.RgbEqual(expected, actual);
        }
    }
}
