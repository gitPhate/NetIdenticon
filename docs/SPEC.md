# NetIdenticon spec

A .NET port of [dgraham/identicon](https://github.com/dgraham/identicon) (Rust, MIT), the GitHub identicon algorithm. Output must match the Rust reference byte for byte.

## Goals

- Library targeting .NET 8 and .NET 10.
- Rendering and PNG encoding via [SkiaSharp](https://github.com/mono/SkiaSharp) (MIT), the only dependency.
- Pixel-identical to the reference for any 16-byte input.

Non-goals: SVG output, custom sizes or colors (may come later), a CLI.

## Public API

```csharp
public static class Identicon
{
    // hash: at least 16 bytes (normally an MD5 digest)
    public static SKBitmap Render(ReadOnlySpan<byte> hash);     // 420×420, caller disposes
    public static byte[] RenderPng(ReadOnlySpan<byte> hash);

    // MD5 of the UTF-8 bytes of text, then RenderPng
    public static byte[] RenderPng(string text);
}
```

Throws `ArgumentException` when `hash` is shorter than 16 bytes.

## Algorithm

Input is the 16-byte hash `s`.

**Grid.** Split `s` into nibbles, high nibble first (`0x2a` → `2, a`). Walk columns `2, 1, 0` (descending) and, in each, rows `0..4`, consuming one nibble per cell. A cell is filled when its nibble is even. Each cell at `(col, row)` is mirrored to `(4 - col, row)`. Only the first 15 nibbles are used.

**Color.** From the last 28 bits:

```
h   = ((s[12] & 0x0f) << 8) | s[13]      // 0..4095
hue = map(h,     0, 4095, 0, 360)
sat = map(s[14], 0, 255,  0, 20)
lum = map(s[15], 0, 255,  0, 20)
fg  = hsl(hue, 65 - sat, 75 - lum)       // CSS3 HSL → RGB
```

`map(v, vmin, vmax, dmin, dmax) = ((v - vmin) * (dmax - dmin)) / ((vmax - vmin) + dmin)`. The `dmin` placement is a bug in the reference. It has no effect because `dmin` is always 0, but port it as written.

**Image.** 420×420, background `(240, 240, 240)`. Each filled cell is a 70×70 square at `(col * 70 + 35, row * 70 + 35)`.

## Fidelity rules

- Do all color math in `float` (`f32` in the reference), never `double`.
- Round channel values with `MathF.Round(v * 255f, MidpointRounding.AwayFromZero)` to match Rust's `round()`.
- The HSL conversion follows the [CSS3 algorithm](https://www.w3.org/TR/css3-color/#hsl-color) exactly as written in `hsl.rs`.

## Rendering and PNG encoding

- Use an `SKBitmap` with `SKColorType.Rgba8888` and `SKAlphaType.Opaque`. Skia has no packed RGB24 type, so alpha is always 255.
- Clear the bitmap to the background color, then fill each cell with `SKCanvas.DrawRect` using integer coordinates and `IsAntialias = false`. This keeps the edges exact.
- Encode with `SKBitmap.Encode(SKEncodedImageFormat.Png, 100)`.
- Only the color math is ported by hand. Skia does the drawing and encoding.

PNG bytes are not expected to match the Rust output because the compressors differ. Compare decoded pixels instead.

Native assets: `SkiaSharp` ships the Windows and macOS binaries. Linux consumers need `SkiaSharp.NativeAssets.Linux` (or the `.NoDependencies` variant for slim containers). Document this in the README rather than referencing it from the library.

## Verification

- Unit tests for the nibble splitter, the HSL conversion (black, white, red, green, blue, as in the reference), and the grid layout.
- Golden tests: generate PNGs with the Rust binary (`printf foo | identicon > foo.png`) for a set of inputs and commit them as test data. Decode each one with `SKBitmap.Decode` and assert that its RGB channels equal those of `Render`.
- Round-trip test: the PNG from `RenderPng` decodes to the same pixels as `Render`.

### Differential tests against the Rust reference

These tests run the Rust implementation in-process and compare its output with ours for arbitrary hash bytes. That covers what the golden files can't: hand-picked edge cases and large random samples.

**Reference source.** Add `dgraham/identicon` as a git submodule at `reference/identicon`, pinned to a specific commit, so both the tests and CI use the exact version we ported.

**Rust wrapper crate** (`tests/reference-ffi/`). This is a `cdylib` crate that depends on the submodule by path and exposes C functions for the C# tests to call:

```rust
// Full image: writes 420*420*3 RGB bytes to out.
#[unsafe(no_mangle)]
pub extern "C" fn identicon_render(hash: *const u8, out: *mut u8);

// Foreground color only: writes 3 RGB bytes to out.
#[unsafe(no_mangle)]
pub extern "C" fn identicon_foreground(hash: *const u8, out: *mut u8);
```

`Identicon::foreground` is private, so `identicon_foreground` pulls in the reference's own `src/hsl.rs` via `#[path]` and repeats the four-line `map` and bit extraction from `lib.rs`. A sweep over every color can't afford a full 420×420 render per input, which is why this second function exists.

**C# side** (`NetIdenticon.ReferenceTests`, a separate project). This project:
- Calls the two functions through `[LibraryImport]`.
- Marks every test `[Trait("Category", "Reference")]` and skips it when the native library isn't present, so `dotnet test` still works without Rust.
- Lets an `IDENTICON_FFI_PATH` environment variable override where the native library is loaded from.

**Tests:**
- Edge hashes: all `0x00`, all `0xff`, alternating nibbles, and each of `s[12..15]` at its minimum and maximum. Full image compared pixel by pixel.
- Random hashes: 10,000 per run with a fixed seed, plus a fresh seed that is logged so a failure can be reproduced. Full image compared.
- Exhaustive grid: all 2¹⁵ nibble-parity patterns. Compare only which cells are filled, by sampling one pixel per cell.
- Exhaustive color: all 2²⁸ values of `(h, s[14], s[15])` against `identicon_foreground`, run in parallel. This is the test that catches `float` and rounding drift. Because it is slow, it lives in a separate `ReferenceExhaustive` category that runs only nightly or when requested.

**CI.** One job installs Rust, runs `cargo build --release` in `tests/reference-ffi`, and then runs `dotnet test --filter Category=Reference`.

## License

MIT. Keep the original copyright notice of dgraham/identicon alongside ours.
