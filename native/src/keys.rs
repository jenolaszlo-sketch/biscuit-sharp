//! Opaque private-key handles over upstream [`biscuit_auth::KeyPair`]
//! (Ed25519, Secp256r1).
//!
//! Private key material never crosses the ABI except through explicit export
//! (PKCS#8 DER, self-describing so import round-trips without an out-of-band
//! algorithm). Import accepts PEM or PKCS#8 DER with upstream auto-detection.
//! Destroying a handle drops the native key; in-memory zeroization is whatever
//! the underlying implementation guarantees on drop — no stronger claim is made.

use std::collections::HashMap;
use std::str::FromStr;
use std::sync::atomic::{AtomicBool, AtomicU64, Ordering};
use std::sync::{LazyLock, Mutex, MutexGuard};

use biscuit_auth::{Algorithm, KeyPair, PrivateKey};

use crate::{
    emit_error, emit_owned, BiscuitSharpBuffer, STATUS_INVALID_INPUT, STATUS_PANIC,
};

pub const OP_KEY_GENERATE: u32 = 1;
pub const OP_KEY_IMPORT: u32 = 2;
pub const OP_KEY_EXPORT_PUBLIC: u32 = 3;
pub const OP_KEY_EXPORT_PRIVATE: u32 = 4;
pub const OP_KEY_DESTROY: u32 = 13;

struct KeyStore {
    next: AtomicU64,
    poisoned: AtomicBool,
    keys: Mutex<HashMap<u64, KeyPair>>,
}

static STORE: LazyLock<KeyStore> = LazyLock::new(|| KeyStore {
    next: AtomicU64::new(1),
    poisoned: AtomicBool::new(false),
    keys: Mutex::new(HashMap::new()),
});

/// Locks the store. On failure the error body is already emitted and the
/// returned status must propagate; a poisoned store stays failed (fail closed).
fn lock_store(output: *mut BiscuitSharpBuffer) -> Result<MutexGuard<'static, HashMap<u64, KeyPair>>, u32> {
    if STORE.poisoned.load(Ordering::SeqCst) {
        return Err(emit_error(
            output,
            STATUS_PANIC,
            "panic",
            "key store is unavailable after a previous failure",
        ));
    }
    STORE.keys.lock().map_err(|_| {
        STORE.poisoned.store(true, Ordering::SeqCst);
        emit_error(
            output,
            STATUS_PANIC,
            "panic",
            "key store lock was poisoned",
        )
    })
}

fn invalid(output: *mut BiscuitSharpBuffer, message: String) -> u32 {
    emit_error(output, STATUS_INVALID_INPUT, "invalid_input", &message)
}

fn key_error(output: *mut BiscuitSharpBuffer, message: String) -> u32 {
    emit_error(output, STATUS_INVALID_INPUT, "key_error", &message)
}

fn parse_request(input: &[u8]) -> Result<serde_json::Value, String> {
    serde_json::from_slice(input).map_err(|e| format!("invalid JSON request: {e}"))
}

fn required_str<'a>(req: &'a serde_json::Value, field: &str) -> Result<&'a str, String> {
    req.get(field)
        .and_then(|v| v.as_str())
        .ok_or_else(|| format!("missing string field '{field}'"))
}

fn required_base64(req: &serde_json::Value, field: &str) -> Result<Vec<u8>, String> {
    let s = required_str(req, field)?;
    base64::decode(s).map_err(|e| format!("field '{field}' is not valid base64: {e}"))
}

fn required_handle(req: &serde_json::Value) -> Result<u64, String> {
    req.get("handle")
        .and_then(|v| v.as_u64())
        .filter(|&h| h != 0)
        .ok_or_else(|| "missing nonzero integer field 'handle'".to_owned())
}

/// Inserts a keypair and reports its handle plus the public half.
fn insert(pair: KeyPair, output: *mut BiscuitSharpBuffer) -> u32 {
    let public = pair.public();
    let algorithm = public.algorithm_string().to_owned();
    let public_bytes = base64::encode(public.to_bytes());
    let mut store = match lock_store(output) {
        Ok(g) => g,
        Err(status) => return status,
    };
    let id = STORE.next.fetch_add(1, Ordering::SeqCst);
    if id == 0 {
        return emit_error(
            output,
            STATUS_PANIC,
            "panic",
            "key handle space exhausted",
        );
    }
    store.insert(id, pair);
    drop(store);
    let body = serde_json::json!({
        "handle": id,
        "algorithm": algorithm,
        "public_key": public_bytes,
    });
    emit_owned(output, body.to_string().into_bytes())
}

/// OP_KEY_GENERATE: `{"algorithm": "ed25519" | "secp256r1"}` (upstream names only).
pub fn op_key_generate(input: &[u8], output: *mut BiscuitSharpBuffer) -> u32 {
    let req = match parse_request(input) {
        Ok(v) => v,
        Err(e) => return invalid(output, e),
    };
    let name = match required_str(&req, "algorithm") {
        Ok(s) => s,
        Err(e) => return invalid(output, e),
    };
    let algorithm = match Algorithm::from_str(name) {
        Ok(a) => a,
        Err(e) => return invalid(output, format!("unknown algorithm '{name}': {e}")),
    };
    insert(KeyPair::new_with_algorithm(algorithm), output)
}

/// OP_KEY_IMPORT: `{"encoding": "pem" | "der", "key": ...}` where a PEM key is
/// a JSON string and a DER key is base64. The algorithm is auto-detected by
/// upstream in both cases.
pub fn op_key_import(input: &[u8], output: *mut BiscuitSharpBuffer) -> u32 {
    let req = match parse_request(input) {
        Ok(v) => v,
        Err(e) => return invalid(output, e),
    };
    let encoding = match required_str(&req, "encoding") {
        Ok(s) => s,
        Err(e) => return invalid(output, e),
    };
    let private = match encoding {
        "pem" => match required_str(&req, "key") {
            Ok(s) => match PrivateKey::from_pem(s) {
                Ok(k) => k,
                Err(e) => return key_error(output, format!("PEM import failed: {e}")),
            },
            Err(e) => return invalid(output, e),
        },
        "der" => match required_base64(&req, "key") {
            Ok(b) => match PrivateKey::from_der(&b) {
                Ok(k) => k,
                Err(e) => return key_error(output, format!("DER import failed: {e}")),
            },
            Err(e) => return invalid(output, e),
        },
        other => {
            return invalid(
                output,
                format!("unsupported key encoding '{other}' (want \"pem\" or \"der\")"),
            );
        }
    };
    insert(KeyPair::from(&private), output)
}

/// OP_KEY_EXPORT_PUBLIC: `{"handle": id}` → `{"algorithm", "public_key"}`.
pub fn op_key_export_public(input: &[u8], output: *mut BiscuitSharpBuffer) -> u32 {
    let req = match parse_request(input) {
        Ok(v) => v,
        Err(e) => return invalid(output, e),
    };
    let handle = match required_handle(&req) {
        Ok(h) => h,
        Err(e) => return invalid(output, e),
    };
    let store = match lock_store(output) {
        Ok(g) => g,
        Err(status) => return status,
    };
    match store.get(&handle) {
        Some(pair) => {
            let public = pair.public();
            let body = serde_json::json!({
                "algorithm": public.algorithm_string(),
                "public_key": base64::encode(public.to_bytes()),
            });
            drop(store);
            emit_owned(output, body.to_string().into_bytes())
        }
        None => invalid(output, "unknown key handle".to_owned()),
    }
}

/// OP_KEY_EXPORT_PRIVATE: `{"handle": id}` → `{"private_key"}` as base64
/// PKCS#8 DER (self-describing; importable without an out-of-band algorithm).
pub fn op_key_export_private(input: &[u8], output: *mut BiscuitSharpBuffer) -> u32 {
    let req = match parse_request(input) {
        Ok(v) => v,
        Err(e) => return invalid(output, e),
    };
    let handle = match required_handle(&req) {
        Ok(h) => h,
        Err(e) => return invalid(output, e),
    };
    let store = match lock_store(output) {
        Ok(g) => g,
        Err(status) => return status,
    };
    match store.get(&handle) {
        Some(pair) => match pair.to_private_key_der() {
            Ok(der) => {
                let body = serde_json::json!({ "private_key": base64::encode(&*der) });
                drop(store);
                emit_owned(output, body.to_string().into_bytes())
            }
            Err(e) => key_error(output, format!("private export failed: {e}")),
        },
        None => invalid(output, "unknown key handle".to_owned()),
    }
}

/// OP_KEY_DESTROY: `{"handle": id}` → `{}`. Dropping the handle drops the
/// native key. Destroying an unknown handle is an error, never silent success.
pub fn op_key_destroy(input: &[u8], output: *mut BiscuitSharpBuffer) -> u32 {
    let req = match parse_request(input) {
        Ok(v) => v,
        Err(e) => return invalid(output, e),
    };
    let handle = match required_handle(&req) {
        Ok(h) => h,
        Err(e) => return invalid(output, e),
    };
    let mut store = match lock_store(output) {
        Ok(g) => g,
        Err(status) => return status,
    };
    match store.remove(&handle) {
        Some(_) => {
            drop(store);
            emit_owned(output, b"{}".to_vec())
        }
        None => invalid(output, "unknown key handle".to_owned()),
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::STATUS_OK;
    use std::ptr;

    /// Calls an op with a JSON body and returns (status, parsed body),
    /// freeing the native buffer exactly once like managed callers must.
    fn call(op: u32, body: serde_json::Value) -> (u32, serde_json::Value) {
        let bytes = body.to_string().into_bytes();
        let mut out = BiscuitSharpBuffer {
            data: ptr::null_mut(),
            len: 0,
        };
        let status = crate::biscuitsharp_call_v1(op, bytes.as_ptr(), bytes.len(), &mut out);
        assert!(!out.data.is_null() && out.len != 0, "op must emit a body");
        let slice =
            unsafe { std::slice::from_raw_parts(out.data as *const u8, out.len) };
        let value: serde_json::Value =
            serde_json::from_slice(slice).expect("output must be valid JSON");
        crate::biscuitsharp_free_v1(out);
        (status, value)
    }

    fn generate(algorithm: &str) -> (u64, String, Vec<u8>) {
        let (status, v) = call(
            OP_KEY_GENERATE,
            serde_json::json!({ "algorithm": algorithm }),
        );
        assert_eq!(status, STATUS_OK, "generate {algorithm}: {v}");
        let handle = v["handle"].as_u64().expect("u64 handle");
        assert_ne!(handle, 0);
        let public = base64::decode(v["public_key"].as_str().expect("public_key")).expect("base64");
        assert!(!public.is_empty());
        (handle, v["algorithm"].as_str().expect("algorithm").to_owned(), public)
    }

    fn destroy(handle: u64) {
        let (status, _) = call(OP_KEY_DESTROY, serde_json::json!({ "handle": handle }));
        assert_eq!(status, STATUS_OK);
    }

    #[test]
    fn generate_reports_both_algorithms_with_distinct_keys() {
        let (h1, alg1, pub1) = generate("ed25519");
        let (h2, alg2, pub2) = generate("ed25519");
        assert_eq!(alg1, "ed25519");
        assert_eq!(alg2, "ed25519");
        assert_eq!(pub1.len(), 32, "raw Ed25519 public key is 32 bytes");
        assert_ne!(pub1, pub2, "fresh keys differ");
        let (h3, alg3, pub3) = generate("secp256r1");
        assert_eq!(alg3, "secp256r1");
        assert!(!pub3.is_empty());
        destroy(h1);
        destroy(h2);
        destroy(h3);
    }

    #[test]
    fn private_der_round_trip_matches_direct_upstream() {
        let (h, alg, public) = generate("ed25519");
        assert_eq!(alg, "ed25519");
        let (status, v) = call(OP_KEY_EXPORT_PRIVATE, serde_json::json!({ "handle": h }));
        assert_eq!(status, STATUS_OK);
        let der = base64::decode(v["private_key"].as_str().expect("private_key")).expect("base64");
        assert!(!der.is_empty());
        // Differential: the same DER through the direct upstream API agrees.
        let direct = KeyPair::from_private_key_der(&der).expect("direct DER import");
        assert_eq!(direct.public().to_bytes(), public);
        // And through the bridge again: same public half, new handle.
        let (status, v2) = call(
            OP_KEY_IMPORT,
            serde_json::json!({ "encoding": "der", "key": base64::encode(&der) }),
        );
        assert_eq!(status, STATUS_OK, "DER re-import: {v2}");
        assert_ne!(v2["handle"].as_u64().unwrap(), h);
        assert_eq!(v2["algorithm"], "ed25519");
        let public2 =
            base64::decode(v2["public_key"].as_str().expect("public_key")).expect("base64");
        assert_eq!(public2, public);
        destroy(h);
        destroy(v2["handle"].as_u64().unwrap());
    }

    #[test]
    fn pem_round_trip_matches_direct_upstream() {
        let direct = KeyPair::new_with_algorithm(Algorithm::Secp256r1);
        let pem = direct
            .to_private_key_pem()
            .expect("direct PEM export")
            .to_string();
        let (status, v) = call(
            OP_KEY_IMPORT,
            serde_json::json!({ "encoding": "pem", "key": pem }),
        );
        assert_eq!(status, STATUS_OK, "PEM import: {v}");
        assert_eq!(v["algorithm"], "secp256r1");
        let public =
            base64::decode(v["public_key"].as_str().expect("public_key")).expect("base64");
        assert_eq!(public, direct.public().to_bytes());
        destroy(v["handle"].as_u64().unwrap());
    }

    #[test]
    fn malformed_key_inputs_fail_with_envelopes() {
        // Unknown algorithm.
        let (status, v) = call(OP_KEY_GENERATE, serde_json::json!({ "algorithm": "rsa" }));
        assert_eq!(status, STATUS_INVALID_INPUT);
        assert_eq!(v["code"], "invalid_input");
        // Missing field.
        let (status, v) = call(OP_KEY_GENERATE, serde_json::json!({}));
        assert_eq!(status, STATUS_INVALID_INPUT);
        assert_eq!(v["code"], "invalid_input");
        // Not JSON at all.
        let mut out = BiscuitSharpBuffer { data: ptr::null_mut(), len: 0 };
        let bytes = b"not json";
        let status = crate::biscuitsharp_call_v1(OP_KEY_GENERATE, bytes.as_ptr(), bytes.len(), &mut out);
        assert_eq!(status, STATUS_INVALID_INPUT);
        crate::biscuitsharp_free_v1(out);
        // Bad base64.
        let (status, v) = call(
            OP_KEY_IMPORT,
            serde_json::json!({ "encoding": "der", "key": "!!!not-base64!!!" }),
        );
        assert_eq!(status, STATUS_INVALID_INPUT);
        assert_eq!(v["code"], "invalid_input");
        // Well-formed base64, undecodable DER.
        let (status, v) = call(
            OP_KEY_IMPORT,
            serde_json::json!({ "encoding": "der", "key": base64::encode(b"garbage") }),
        );
        assert_eq!(status, STATUS_INVALID_INPUT);
        assert_eq!(v["code"], "key_error");
        // Unknown encoding.
        let (status, v) = call(
            OP_KEY_IMPORT,
            serde_json::json!({ "encoding": "raw", "key": base64::encode(b"garbage") }),
        );
        assert_eq!(status, STATUS_INVALID_INPUT);
        assert_eq!(v["code"], "invalid_input");
        // Unknown handles.
        for op in [OP_KEY_EXPORT_PUBLIC, OP_KEY_EXPORT_PRIVATE, OP_KEY_DESTROY] {
            let (status, v) = call(op, serde_json::json!({ "handle": u64::MAX }));
            assert_eq!(status, STATUS_INVALID_INPUT, "op {op}");
            assert_eq!(v["code"], "invalid_input");
        }
        // Zero handle is never valid.
        let (status, _) = call(OP_KEY_DESTROY, serde_json::json!({ "handle": 0 }));
        assert_eq!(status, STATUS_INVALID_INPUT);
    }

    #[test]
    fn use_after_destroy_fails() {
        let (h, _, _) = generate("ed25519");
        destroy(h);
        let (status, v) = call(OP_KEY_EXPORT_PUBLIC, serde_json::json!({ "handle": h }));
        assert_eq!(status, STATUS_INVALID_INPUT);
        assert_eq!(v["code"], "invalid_input");
        let (status, _) = call(OP_KEY_DESTROY, serde_json::json!({ "handle": h }));
        assert_eq!(status, STATUS_INVALID_INPUT, "double destroy is an error");
        let _ = v;
    }

    #[test]
    fn concurrent_generate_export_destroy() {
        std::thread::scope(|s| {
            for _ in 0..8 {
                s.spawn(|| {
                    for _ in 0..25 {
                        let (h, _, public) = generate("ed25519");
                        let (status, v) = call(
                            OP_KEY_EXPORT_PUBLIC,
                            serde_json::json!({ "handle": h }),
                        );
                        assert_eq!(status, STATUS_OK);
                        let again = base64::decode(
                            v["public_key"].as_str().expect("public_key"),
                        )
                        .expect("base64");
                        assert_eq!(again, public);
                        destroy(h);
                    }
                });
            }
        });
    }
}
