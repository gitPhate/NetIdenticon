using System.Collections.Concurrent;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace NetIdenticon.ReferenceTests;

/// <summary>A fact that is skipped when the native reference library is not present.</summary>
public sealed class RefFactAttribute : FactAttribute
{
    public RefFactAttribute()
    {
        if (!Native.Available)
            Skip = "Native reference library not found (build src/reference-ffi or set IDENTICON_FFI_PATH).";
    }
}

/// <summary>A theory that is skipped when the native reference library is not present.</summary>
public sealed class RefTheoryAttribute : TheoryAttribute
{
    public RefTheoryAttribute()
    {
        if (!Native.Available)
            Skip = "Native reference library not found (build src/reference-ffi or set IDENTICON_FFI_PATH).";
    }
}

internal static class Compare
{
    public static void Images(byte[] hash)
    {
        byte[] expected = Native.Render(hash);
        using SKBitmap actual = Identicon.Render(hash);
        for (int y = 0; y < 420; y++)
        {
            for (int x = 0; x < 420; x++)
            {
                SKColor c = actual.GetPixel(x, y);
                int i = (y * 420 + x) * 3;
                if (c.Red != expected[i] || c.Green != expected[i + 1] || c.Blue != expected[i + 2])
                    Assert.Fail($"Hash {Convert.ToHexString(hash)}: pixel ({x},{y}) expected " +
                                $"({expected[i]},{expected[i + 1]},{expected[i + 2]}) got ({c.Red},{c.Green},{c.Blue})");
            }
        }
    }
}

[Trait("Category", "Reference")]
public class EdgeHashTests
{
    public static IEnumerable<object[]> Hashes() =>
        Candidates().DistinctBy(h => Convert.ToHexString(h)).Select(h => new object[] { h });

    private static IEnumerable<byte[]> Candidates()
    {
        yield return Filled(0x00);
        yield return Filled(0xff);
        yield return Filled(0x0f);
        yield return Filled(0xf0);
        yield return Filled(0xa5);
        yield return Filled(0x5a);
        for (int i = 12; i < 16; i++)
        {
            yield return With(i, 0x00);
            yield return With(i, 0xff);
        }
        yield return With(12, 0x0f);
    }

    private static byte[] Filled(byte v) { byte[] h = new byte[16]; Array.Fill(h, v); return h; }
    private static byte[] With(int index, byte v) { byte[] h = Filled(0x00); h[index] = v; return h; }

    [RefTheory]
    [MemberData(nameof(Hashes))]
    public void FullImageMatches(byte[] hash) => Compare.Images(hash);
}

[Trait("Category", "Reference")]
public class RandomHashTests(ITestOutputHelper output)
{
    [RefFact]
    public void FixedSeed() => Run(12345, 10_000);

    [RefFact]
    public void FreshSeed()
    {
        int seed = Environment.TickCount;
        output.WriteLine($"Seed: {seed}");
        Run(seed, 10_000);
    }

    private static void Run(int seed, int count)
    {
        Random rng = new(seed);
        byte[] hash = new byte[16];
        for (int n = 0; n < count; n++)
        {
            rng.NextBytes(hash);
            Compare.Images(hash);
        }
    }
}

[Trait("Category", "Reference")]
public class ExhaustiveGridTests
{
    [RefFact]
    public void AllNibbleParityPatterns()
    {
        Parallel.For(0, 1 << 15, pattern =>
        {
            // Nibble i is even iff bit i of pattern is set.
            byte[] hash = new byte[16];
            for (int i = 0; i < 15; i++)
            {
                int nibble = ((pattern >> i) & 1) == 1 ? 0x0 : 0x1;
                if (i % 2 == 0) hash[i / 2] |= (byte)(nibble << 4);
                else hash[i / 2] |= (byte)nibble;
            }

            byte[] expected = Native.Render(hash);
            using SKBitmap actual = Identicon.Render(hash);
            for (int row = 0; row < 5; row++)
            {
                for (int col = 0; col < 5; col++)
                {
                    int x = col * 70 + 35 + 35, y = row * 70 + 35 + 35;
                    int i = (y * 420 + x) * 3;
                    SKColor c = actual.GetPixel(x, y);
                    bool refFilled = expected[i] != 240 || expected[i + 1] != 240 || expected[i + 2] != 240;
                    bool ourFilled = c.Red != 240 || c.Green != 240 || c.Blue != 240;
                    Assert.True(refFilled == ourFilled, $"Pattern {pattern}: cell ({col},{row}) differs");
                }
            }
        });
    }
}

[Trait("Category", "ReferenceExhaustive")]
public class ExhaustiveColorTests
{
    [RefFact]
    public void AllHueSatLumValues()
    {
        // 12 bits of hue (s[12] low nibble + s[13]), s[14], s[15] => 2^28 combinations.
        ConcurrentBag<string> failures = new();
        Parallel.For(0, 1 << 12, h =>
        {
            byte[] hash = new byte[16];
            hash[12] = (byte)(h >> 8);
            hash[13] = (byte)(h & 0xff);
            for (int s = 0; s < 256; s++)
            {
                hash[14] = (byte)s;
                for (int l = 0; l < 256; l++)
                {
                    hash[15] = (byte)l;
                    (byte, byte, byte) expected = Native.Foreground(hash);
                    (byte, byte, byte) actual = Identicon.Foreground(hash);
                    if (expected != actual && failures.Count < 20)
                        failures.Add($"h={h} s={s} l={l}: expected {expected} got {actual}");
                }
            }
        });
        Assert.True(failures.IsEmpty, string.Join(Environment.NewLine, failures));
    }
}
