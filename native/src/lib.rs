//! BiscuitSharp native bridge (ABI 1) — scaffolding.
//!
//! M1 implements the stable C ABI over biscuit-auth 6.0.0:
//! pointer+length inputs, native-owned output buffers with exactly one free,
//! caught recoverable panics (never unwind across FFI), bounded I/O,
//! and ABI + full upstream version identity. Managed callers must never
//! depend on Rust ABI details.

use std::slice;

/// ABI version served by this bridge.
pub const ABI_VERSION: u32 = 1;
/// Bridge implementation version.
pub const BRIDGE_VERSION: &str = env!("BISCUITSHARP_BRIDGE_VERSION");
/// Input bound: 16 MiB wire messages.
pub const MAX_INPUT_BYTES: usize = 16 * 1024 * 1024;
/// Output bound: 64 MiB wire messages.
pub const MAX_OUTPUT_BYTES: usize = 64 * 1024 * 1024;

/// C-compatible output buffer: one owned boxed byte slice, freed exactly once
/// with [`biscuitsharp_free_v1`] using the unmodified pointer/length.
#[repr(C)]
pub struct BiscuitSharpBuffer {
    pub data: *mut u8,
    pub len: usize,
}

/// Operation ids (M1 fills in real handlers; scaffolding returns unsupported).
pub const OP_VERSION: u32 = 0;
// Reserved: 1=key_generate, 2=key_import, 3=key_export_public, 4=key_export_private,
// 5=token_create, 6=token_parse_verify, 7=token_serialize, 8=token_attenuate,
// 9=token_seal, 10=token_authorize, 11=token_revocation_ids, 12=token_inspect.

/// Status codes: 0 ok (including Deny answers), 1 invalid input, 2 unsupported op,
/// 3 caught panic, 4 oversized output.
pub const STATUS_OK: u32 = 0;
pub const STATUS_INVALID_INPUT: u32 = 1;
pub const STATUS_UNSUPPORTED_OP: u32 = 2;
pub const STATUS_PANIC: u32 = 3;
pub const STATUS_OVERSIZED: u32 = 4;

/// Returns the bridge ABI version.
#[no_mangle]
pub extern "C" fn biscuitsharp_abi_version() -> u32 {
    ABI_VERSION
}

/// Stable dispatch entry point. Always initializes `output` first; never unwinds.
#[no_mangle]
pub extern "C" fn biscuitsharp_call_v1(
    operation: u32,
    input: *const u8,
    input_len: usize,
    output: *mut BiscuitSharpBuffer,
) -> u32 {
    if output.is_null() {
        return STATUS_INVALID_INPUT;
    }
    unsafe {
        (*output).data = std::ptr::null_mut();
        (*output).len = 0;
    }
    let result = std::panic::catch_unwind(|| dispatch(operation, input, input_len, output));
    match result {
        Ok(status) => status,
        Err(_) => STATUS_PANIC,
    }
}

fn dispatch(
    operation: u32,
    input: *const u8,
    input_len: usize,
    output: *mut BiscuitSharpBuffer,
) -> u32 {
    if input_len > MAX_INPUT_BYTES {
        return STATUS_INVALID_INPUT;
    }
    if input.is_null() && input_len != 0 {
        return STATUS_INVALID_INPUT;
    }
    let _input: &[u8] = if input_len == 0 {
        &[]
    } else {
        // SAFETY: caller guarantees readable input for the duration of the call.
        unsafe { slice::from_raw_parts(input, input_len) }
    };
    let _ = output;
    match operation {
        OP_VERSION => STATUS_UNSUPPORTED_OP, // M1: return version/identity JSON.
        _ => STATUS_UNSUPPORTED_OP,
    }
}

/// Frees a buffer produced by [`biscuitsharp_call_v1`]. Null/zero buffers are safe.
#[no_mangle]
pub extern "C" fn biscuitsharp_free_v1(buffer: BiscuitSharpBuffer) {
    if buffer.data.is_null() || buffer.len == 0 {
        return;
    }
    // SAFETY: buffer originates from this library as a boxed slice with this exact layout.
    unsafe {
        let _ = Box::from_raw(slice::from_raw_parts_mut(buffer.data, buffer.len));
    }
}

#[allow(dead_code)]
fn emit_owned(output: *mut BiscuitSharpBuffer, bytes: Vec<u8>) -> u32 {
    if bytes.len() > MAX_OUTPUT_BYTES {
        return STATUS_OVERSIZED;
    }
    let mut boxed = bytes.into_boxed_slice();
    unsafe {
        (*output).data = boxed.as_mut_ptr();
        (*output).len = boxed.len();
        std::mem::forget(boxed);
    }
    STATUS_OK
}
