//! BiscuitSharp native bridge (ABI 1) over biscuit-auth 6.0.0.
//!
//! Stable C ABI: pointer+length inputs, native-owned output buffers with exactly
//! one free, caught recoverable panics (never unwind across FFI), bounded I/O,
//! and ABI + full upstream version identity. Managed callers must never depend
//! on Rust ABI details.

use std::slice;

/// ABI version served by this bridge.
pub const ABI_VERSION: u32 = 1;
/// Bridge implementation version.
pub const BRIDGE_VERSION: &str = env!("BISCUITSHARP_BRIDGE_VERSION");
/// biscuit-auth version read from Cargo.lock at build time.
pub const BISCUIT_AUTH_VERSION: &str = env!("BISCUITSHARP_BISCUIT_AUTH_VERSION");
/// Upstream source commit. Verified equal to tag `biscuit-auth-6.0.0`
/// (see docs/native-boundary.md); re-verify on every upgrade.
pub const UPSTREAM_COMMIT: &str = "0f0b4e0e6fe07220c1ba6b51bff21d450d94a975";
/// Recorded token/spec versions. biscuit-auth keeps these in its private
/// `token` module (`src/token/mod.rs:35-43` at the pinned commit), so the
/// bridge records the inspected values and the upgrade procedure re-checks
/// them, exactly like [`UPSTREAM_COMMIT`].
pub const MIN_SCHEMA_VERSION: u32 = 3;
pub const MAX_SCHEMA_VERSION: u32 = 6;
/// Schema version carrying Datalog 3.3 semantics.
pub const DATALOG_3_3: u32 = 6;
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

mod keys;
mod tokens;
mod authorizer;

#[cfg(test)]
mod adversarial;

/// Operation ids. Key operations live in [`keys`], token operations in
/// [`tokens`], authorization in [`authorizer`].
/// Reserved: 7=token_serialize (unneeded: managed tokens hold canonical bytes).
pub const OP_VERSION: u32 = 0;

/// Status codes: 0 ok (including Deny answers once authorize lands), 1 invalid
/// input, 2 unsupported operation, 3 caught panic, 4 oversized output.
/// Every nonzero status also carries a JSON `{"code","message"}` body, except
/// the panic path, which leaves the pre-initialized empty buffer (allocating
/// after an unwind is not safe to assume).
pub const STATUS_OK: u32 = 0;
pub const STATUS_INVALID_INPUT: u32 = 1;
pub const STATUS_UNSUPPORTED_OP: u32 = 2;
pub const STATUS_PANIC: u32 = 3;
pub const STATUS_OVERSIZED: u32 = 4;

/// Test-only operation that panics, exercising FFI panic containment
/// (`catch_unwind` → `STATUS_PANIC` with an empty body). Never dispatched in
/// shipped builds (`#[cfg(test)]`).
#[cfg(test)]
pub const OP_TEST_PANIC: u32 = 0xFFFF_FFF1;

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
        return emit_error(
            output,
            STATUS_INVALID_INPUT,
            "invalid_input",
            "input exceeds the 16 MiB bridge bound",
        );
    }
    if input.is_null() && input_len != 0 {
        return emit_error(
            output,
            STATUS_INVALID_INPUT,
            "invalid_input",
            "null input with nonzero length",
        );
    }
    let bytes: &[u8] = if input_len == 0 {
        &[]
    } else {
        // SAFETY: caller guarantees readable input for the duration of the call.
        unsafe { slice::from_raw_parts(input, input_len) }
    };
    #[cfg(test)]
    if operation == OP_TEST_PANIC {
        panic!("intentional test panic");
    }
    match operation {
        OP_VERSION => op_version(bytes, output),
        keys::OP_KEY_GENERATE => keys::op_key_generate(bytes, output),
        keys::OP_KEY_IMPORT => keys::op_key_import(bytes, output),
        keys::OP_KEY_EXPORT_PUBLIC => keys::op_key_export_public(bytes, output),
        keys::OP_KEY_EXPORT_PRIVATE => keys::op_key_export_private(bytes, output),
        keys::OP_KEY_DESTROY => keys::op_key_destroy(bytes, output),
        tokens::OP_TOKEN_CREATE => tokens::op_token_create(bytes, output),
        tokens::OP_TOKEN_PARSE_VERIFY => tokens::op_token_parse_verify(bytes, output),
        tokens::OP_TOKEN_ATTENUATE => tokens::op_token_attenuate(bytes, output),
        tokens::OP_TOKEN_SEAL => tokens::op_token_seal(bytes, output),
        tokens::OP_TOKEN_REVOCATION_IDS => tokens::op_token_revocation_ids(bytes, output),
        tokens::OP_TOKEN_INSPECT => tokens::op_token_inspect(bytes, output),
        authorizer::OP_TOKEN_AUTHORIZE => authorizer::op_token_authorize(bytes, output),
        _ => emit_error(
            output,
            STATUS_UNSUPPORTED_OP,
            "unsupported_operation",
            "unknown operation id",
        ),
    }
}

/// OP_VERSION: reports the loaded asset identity. Takes no input.
fn op_version(input: &[u8], output: *mut BiscuitSharpBuffer) -> u32 {
    if !input.is_empty() {
        return emit_error(
            output,
            STATUS_INVALID_INPUT,
            "invalid_input",
            "version takes no input",
        );
    }
    let features: Vec<&str> = env!("BISCUITSHARP_ENABLED_FEATURES").split(',').collect();
    let body = serde_json::json!({
        "biscuit_auth_version": BISCUIT_AUTH_VERSION,
        "min_schema_version": MIN_SCHEMA_VERSION,
        "max_schema_version": MAX_SCHEMA_VERSION,
        "datalog_3_3_schema_version": DATALOG_3_3,
        "datalog": "3.3",
        "bridge_version": BRIDGE_VERSION,
        "abi_version": ABI_VERSION,
        "rust_version": env!("BISCUITSHARP_RUST_VERSION"),
        "target_triple": env!("BISCUITSHARP_TARGET_TRIPLE"),
        "enabled_features": features,
        "upstream_commit": UPSTREAM_COMMIT,
        "cargo_lock_sha256": env!("BISCUITSHARP_CARGO_LOCK_SHA256"),
    });
    emit_owned(output, body.to_string().into_bytes())
}

fn emit_error(
    output: *mut BiscuitSharpBuffer,
    status: u32,
    code: &str,
    message: &str,
) -> u32 {
    let body = serde_json::json!({ "code": code, "message": message });
    let emit = emit_owned(output, body.to_string().into_bytes());
    if emit != STATUS_OK {
        return emit;
    }
    status
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

#[cfg(test)]
mod tests {
    use super::*;
    use std::ptr;

    /// Reads the buffer and frees it exactly once, mirroring the managed contract.
    fn take_json(out: BiscuitSharpBuffer) -> serde_json::Value {
        assert!(!out.data.is_null() && out.len != 0);
        let bytes: &[u8] = unsafe { slice::from_raw_parts(out.data as *const u8, out.len) };
        let value: serde_json::Value =
            serde_json::from_slice(bytes).expect("output must be valid JSON");
        biscuitsharp_free_v1(out);
        value
    }

    #[test]
    fn version_reports_loaded_identity() {
        let mut out = BiscuitSharpBuffer {
            data: ptr::null_mut(),
            len: 0,
        };
        let status = biscuitsharp_call_v1(OP_VERSION, ptr::null(), 0, &mut out);
        assert_eq!(status, STATUS_OK);
        let v = take_json(out);
        assert_eq!(v["biscuit_auth_version"], "6.0.0");
        assert_eq!(v["abi_version"], 1);
        assert_eq!(v["bridge_version"], "0.1.0");
        assert_eq!(v["min_schema_version"], 3);
        assert_eq!(v["max_schema_version"], 6);
        assert_eq!(v["datalog_3_3_schema_version"], 6);
        assert_eq!(v["datalog"], "3.3");
        assert_eq!(v["upstream_commit"], UPSTREAM_COMMIT);
        assert_eq!(v["upstream_commit"].as_str().unwrap().len(), 40);
        assert_eq!(v["cargo_lock_sha256"].as_str().unwrap().len(), 64);
        let features = v["enabled_features"].as_array().expect("features array");
        assert!(features.iter().any(|f| f == "pem"));
        assert!(!v["rust_version"].as_str().unwrap().is_empty());
        assert!(!v["target_triple"].as_str().unwrap().is_empty());
    }

    #[test]
    fn unknown_operation_is_unsupported_with_json_body() {
        let mut out = BiscuitSharpBuffer {
            data: ptr::null_mut(),
            len: 0,
        };
        let status = biscuitsharp_call_v1(0xFFFF_FFFF, ptr::null(), 0, &mut out);
        assert_eq!(status, STATUS_UNSUPPORTED_OP);
        let v = take_json(out);
        assert_eq!(v["code"], "unsupported_operation");
    }

    #[test]
    fn version_rejects_nonempty_input() {
        let input = [0x7Bu8];
        let mut out = BiscuitSharpBuffer {
            data: ptr::null_mut(),
            len: 0,
        };
        let status = biscuitsharp_call_v1(OP_VERSION, input.as_ptr(), input.len(), &mut out);
        assert_eq!(status, STATUS_INVALID_INPUT);
        let v = take_json(out);
        assert_eq!(v["code"], "invalid_input");
    }

    #[test]
    fn null_output_is_invalid_input_without_crash() {
        let status = biscuitsharp_call_v1(OP_VERSION, ptr::null(), 0, ptr::null_mut());
        assert_eq!(status, STATUS_INVALID_INPUT);
    }

    #[test]
    fn oversized_input_is_rejected_before_read() {
        let mut out = BiscuitSharpBuffer {
            data: ptr::null_mut(),
            len: 0,
        };
        // Null with huge length: rejected on the bound, never dereferenced.
        let status =
            biscuitsharp_call_v1(OP_VERSION, ptr::null(), MAX_INPUT_BYTES + 1, &mut out);
        assert_eq!(status, STATUS_INVALID_INPUT);
        let v = take_json(out);
        assert_eq!(v["code"], "invalid_input");
    }

    #[test]
    fn free_accepts_null_and_zero_buffers() {
        biscuitsharp_free_v1(BiscuitSharpBuffer {
            data: ptr::null_mut(),
            len: 0,
        });
    }
}
