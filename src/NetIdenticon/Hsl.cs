namespace NetIdenticon;

/// <summary>CSS3 HSL to RGB, ported from hsl.rs. All math is float to match f32.</summary>
internal static class Hsl
{
    // http://www.w3.org/TR/css3-color/#hsl-color
    public static (byte R, byte G, byte B) ToRgb(float hue, float sat, float lum)
    {
        hue /= 360.0f;
        sat /= 100.0f;
        lum /= 100.0f;

        float b = lum <= 0.5f
            ? lum * (sat + 1.0f)
            : lum + sat - lum * sat;
        float a = lum * 2.0f - b;

        float r = HueToRgb(a, b, hue + 1.0f / 3.0f);
        float g = HueToRgb(a, b, hue);
        float bl = HueToRgb(a, b, hue - 1.0f / 3.0f);

        return (Channel(r), Channel(g), Channel(bl));
    }

    private static byte Channel(float v) =>
        (byte)MathF.Round(v * 255.0f, MidpointRounding.AwayFromZero);

    private static float HueToRgb(float a, float b, float hue)
    {
        float h;
        if (hue < 0.0f) h = hue + 1.0f;
        else if (hue > 1.0f) h = hue - 1.0f;
        else h = hue;

        if (h < 1.0f / 6.0f) return a + (b - a) * 6.0f * h;
        if (h < 1.0f / 2.0f) return b;
        if (h < 2.0f / 3.0f) return a + (b - a) * (2.0f / 3.0f - h) * 6.0f;
        return a;
    }
}
