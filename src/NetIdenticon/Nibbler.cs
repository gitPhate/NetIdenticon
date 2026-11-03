namespace NetIdenticon;

internal static class Nibbler
{
    /// <summary>Splits bytes into nibbles, high nibble first.</summary>
    public static byte[] Split(ReadOnlySpan<byte> bytes)
    {
        byte[] result = new byte[bytes.Length * 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            result[i * 2] = (byte)(bytes[i] >> 4);
            result[i * 2 + 1] = (byte)(bytes[i] & 0x0f);
        }
        return result;
    }
}
