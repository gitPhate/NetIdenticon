# NetIdenticon

A .NET 8 / .NET 10 port of [dgraham/identicon](https://github.com/dgraham/identicon), the GitHub identicon algorithm. Pixel-identical to the Rust reference for any 16-byte hash. Rendering and PNG encoding use [SkiaSharp](https://github.com/mono/SkiaSharp).

```csharp
byte[] png = Identicon.RenderPng("foo");                 // MD5 of the UTF-8 text
byte[] png2 = Identicon.RenderPng(md5Hash);              // 16+ byte hash
using SKBitmap bitmap = Identicon.Render(md5Hash);       // 420x420, caller disposes
```

Throws `ArgumentException` when the hash is shorter than 16 bytes.

## Native assets

`SkiaSharp` ships the Windows and macOS binaries. On Linux, add `SkiaSharp.NativeAssets.Linux` (or `SkiaSharp.NativeAssets.Linux.NoDependencies` for slim containers) to your application.

## Tests

- `dotnet test src/NetIdenticon.Tests`: unit, golden and round-trip tests.
- Golden files: put PNGs made with `printf foo | identicon > foo.png` in `src/NetIdenticon.Tests/golden/` (file name = input text).
- Differential tests against the Rust reference:
  ```
  git submodule update --init
  cargo build --release --manifest-path src/reference-ffi/Cargo.toml
  dotnet test src/NetIdenticon.ReferenceTests --filter Category=Reference
  ```
  Set `IDENTICON_FFI_PATH` to load the native library from elsewhere. Without the library these tests are skipped. The 2^28 color sweep runs with `--filter Category=ReferenceExhaustive`.

## License

MIT. See [LICENSE](LICENSE), which also carries the original dgraham/identicon notice.
