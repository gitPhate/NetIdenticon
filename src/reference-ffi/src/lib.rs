// Pull in the reference's own HSL conversion (Identicon::foreground is private).
#[path = "../../../reference/identicon/src/hsl.rs"]
mod hsl;

use identicon::Identicon;

/// Full image: writes 420*420*3 RGB bytes to `out`. `hash` must point to 16 bytes.
#[unsafe(no_mangle)]
pub extern "C" fn identicon_render(hash: *const u8, out: *mut u8) {
    let source = unsafe { std::slice::from_raw_parts(hash, 16) };
    let image = Identicon::new(source).image();
    let raw = image.as_raw();
    unsafe { std::ptr::copy_nonoverlapping(raw.as_ptr(), out, raw.len()) };
}

// https://processing.org/reference/map_.html (copied from the reference's lib.rs)
fn map(value: u32, vmin: u32, vmax: u32, dmin: u32, dmax: u32) -> f32 {
    ((value - vmin) * (dmax - dmin)) as f32 / ((vmax - vmin) + dmin) as f32
}

/// Foreground color only: writes 3 RGB bytes to `out`. `hash` must point to 16 bytes.
#[unsafe(no_mangle)]
pub extern "C" fn identicon_foreground(hash: *const u8, out: *mut u8) {
    let source = unsafe { std::slice::from_raw_parts(hash, 16) };

    let h1 = (source[12] as u16 & 0x0f) << 8;
    let h2 = source[13] as u16;
    let h = (h1 | h2) as u32;
    let s = source[14] as u32;
    let l = source[15] as u32;

    let hue = map(h, 0, 4095, 0, 360);
    let sat = map(s, 0, 255, 0, 20);
    let lum = map(l, 0, 255, 0, 20);

    let rgb = hsl::Hsl::new(hue, 65.0 - sat, 75.0 - lum).rgb();
    unsafe { std::ptr::copy_nonoverlapping(rgb.0.as_ptr(), out, 3) };
}
