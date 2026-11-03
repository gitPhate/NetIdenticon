using System.Reflection;
using System.Runtime.InteropServices;

namespace NetIdenticon.ReferenceTests;

internal static partial class Native
{
    private const string Lib = "identicon_reference_ffi";

    static Native()
    {
        NativeLibrary.SetDllImportResolver(typeof(Native).Assembly, Resolve);
    }

    public static string? FindLibrary()
    {
        string? env = Environment.GetEnvironmentVariable("IDENTICON_FFI_PATH");
        if (!string.IsNullOrEmpty(env)) return File.Exists(env) ? env : null;

        string file = OperatingSystem.IsWindows() ? $"{Lib}.dll"
            : OperatingSystem.IsMacOS() ? $"lib{Lib}.dylib"
            : $"lib{Lib}.so";

        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "src", "reference-ffi", "target", "release", file);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    public static bool Available => FindLibrary() != null;

    private static IntPtr Resolve(string name, Assembly assembly, DllImportSearchPath? path)
    {
        if (name != Lib) return IntPtr.Zero;
        string? p = FindLibrary();
        return p == null ? IntPtr.Zero : NativeLibrary.Load(p);
    }

    [LibraryImport(Lib)]
    private static unsafe partial void identicon_render(byte* hash, byte* output);

    [LibraryImport(Lib)]
    private static unsafe partial void identicon_foreground(byte* hash, byte* output);

    public static unsafe byte[] Render(ReadOnlySpan<byte> hash)
    {
        byte[] result = new byte[420 * 420 * 3];
        fixed (byte* h = hash)
        fixed (byte* o = result)
            identicon_render(h, o);
        return result;
    }

    public static unsafe (byte R, byte G, byte B) Foreground(ReadOnlySpan<byte> hash)
    {
        byte* o = stackalloc byte[3];
        fixed (byte* h = hash)
            identicon_foreground(h, o);
        return (o[0], o[1], o[2]);
    }
}
