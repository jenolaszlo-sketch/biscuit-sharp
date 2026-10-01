//! Deterministic adversarial bridge inputs (M2.5 graduation hardening):
//! 4,096 mutated tokens, Datalog sources, key encodings, and envelopes.
//!
//! Precise contract per category: malformed input fails closed; success is
//! legitimate only with verified, well-formed output (canonical re-verifying
//! bytes, usable key handles). Upstream framing tolerates trailing bytes, so
//! `extend` mutants verify — parse always returns canonical bytes, and an
//! authorization allow on mutated input requires byte-identical canonical
//! content (signature forgery stays impossible). Panics would surface as
//! status 3 with an empty body; the matrix counts them and asserts zero.
//! Deterministic xorshift stream: same seed, same cases, every run, every
//! machine.

use std::ptr;

use crate::{BiscuitSharpBuffer, STATUS_INVALID_INPUT, STATUS_OK, STATUS_PANIC};

/// Deterministic PRNG: xorshift64*, fixed seed per run category.
struct Rng(u64);

impl Rng {
    fn next(&mut self) -> u64 {
        let mut x = self.0;
        x ^= x >> 12;
        x ^= x << 25;
        x ^= x >> 27;
        self.0 = x;
        x.wrapping_mul(0x2545_F491_4F6C_DD1D)
    }

    fn below(&mut self, n: usize) -> usize {
        (self.next() % (n as u64)) as usize
    }
}

fn call_bytes(op: u32, bytes: &[u8]) -> (u32, Option<serde_json::Value>) {
    let mut out = BiscuitSharpBuffer {
        data: ptr::null_mut(),
        len: 0,
    };
    // SAFETY: input is borrowed for the call; output is a valid writable struct.
    let status = unsafe { crate::biscuitsharp_call_v1(op, bytes.as_ptr(), bytes.len(), &mut out) };
    let value = if out.data.is_null() || out.len == 0 {
        None
    } else {
        let slice = unsafe { std::slice::from_raw_parts(out.data as *const u8, out.len) };
        Some(serde_json::from_slice(slice).expect("output must be valid JSON"))
    };
    crate::biscuitsharp_free_v1(out);
    (status, value)
}

fn call(op: u32, body: &serde_json::Value) -> (u32, serde_json::Value) {
    let (status, value) = call_bytes(op, body.to_string().as_bytes());
    (status, value.expect("op must emit a body"))
}

/// Applies a deterministic byte mutation chosen by the stream.
fn mutate_bytes(rng: &mut Rng, bytes: &[u8]) -> Vec<u8> {
    mutate_traced(rng, bytes).0
}

fn mutate_traced(rng: &mut Rng, bytes: &[u8]) -> (Vec<u8>, &'static str) {
    if bytes.is_empty() {
        return (vec![0xFF], "empty");
    }
    let mut out = bytes.to_vec();
    let strategy = match rng.below(6) {
        0 => {
            // Flip one random bit.
            let i = rng.below(out.len());
            out[i] ^= 1 << rng.below(8);
            "bitflip"
        }
        1 => {
            // Truncate (possibly to empty).
            out.truncate(rng.below(out.len()));
            "truncate"
        }
        2 => {
            // Extend with random bytes.
            let extra = 1 + rng.below(16);
            out.extend((0..extra).map(|_| rng.next() as u8));
            "extend"
        }
        3 => {
            // Splice a random span (bounds-checked against the tail).
            let start = rng.below(out.len());
            let max = out.len() - start;
            let len = 1 + rng.below(8.min(max));
            for slot in out.iter_mut().skip(start).take(len) {
                *slot = rng.next() as u8;
            }
            "splice"
        }
        4 => {
            // Drop one random byte.
            out.remove(rng.below(out.len()));
            "drop"
        }
        _ => {
            // Set one random byte to an extreme.
            let i = rng.below(out.len());
            out[i] = if rng.below(2) == 0 { 0x00 } else { 0xFF };
            "extreme"
        }
    };
    (out, strategy)
}

struct Corpus {
    root: serde_json::Value,
    token: Vec<u8>,
    der: Vec<u8>,
    pem: String,
}

/// Builds the fixed valid corpus through the bridge (black-box, as managed
/// callers see it).
fn corpus() -> Corpus {
    let (status, v) = call(
        crate::keys::OP_KEY_GENERATE,
        &serde_json::json!({ "algorithm": "ed25519" }),
    );
    assert_eq!(status, STATUS_OK);
    let root = serde_json::json!({
        "algorithm": v["algorithm"],
        "public_key": v["public_key"],
    });
    let (status, v) = call(
        crate::tokens::OP_TOKEN_CREATE,
        &serde_json::json!({
            "root_handle": v["handle"],
            "facts": [{ "source": "right(\"workspace.main\", \"read\")" }],
        }),
    );
    assert_eq!(status, STATUS_OK);
    let token = base64::decode(v["token"].as_str().unwrap()).unwrap();
    // Keep the handle alive in the store for the whole run; fetch DER via a
    // throwaway key instead so the corpus root stays usable.
    let (status, v) = call(
        crate::keys::OP_KEY_GENERATE,
        &serde_json::json!({ "algorithm": "ed25519" }),
    );
    assert_eq!(status, STATUS_OK);
    let der_handle = v["handle"].as_u64().unwrap();
    let (status, v) = call(
        crate::keys::OP_KEY_EXPORT_PRIVATE,
        &serde_json::json!({ "handle": der_handle }),
    );
    assert_eq!(status, STATUS_OK);
    let der = base64::decode(v["private_key"].as_str().unwrap()).unwrap();
    let (status, _) = call(
        crate::keys::OP_KEY_DESTROY,
        &serde_json::json!({ "handle": der_handle }),
    );
    assert_eq!(status, STATUS_OK);
    // PKCS#8 PEM armor around the DER, exactly like upstream export.
    let mut pem = String::from("-----BEGIN PRIVATE KEY-----\n");
    let b64 = base64::encode(&der);
    for chunk in b64.as_bytes().chunks(64) {
        pem.push_str(std::str::from_utf8(chunk).unwrap());
        pem.push('\n');
    }
    pem.push_str("-----END PRIVATE KEY-----\n");
    Corpus {
        root,
        token,
        der,
        pem,
    }
}

fn allow_request(token: &[u8], root: &serde_json::Value) -> serde_json::Value {
    serde_json::json!({
        "token": base64::encode(token),
        "root": root,
        "facts": ["operation(\"read\")"],
        "checks": [],
        "policies": ["allow if right(\"workspace.main\", \"read\");"],
    })
}

fn parses(token: &[u8], root: &serde_json::Value) -> Option<Vec<u8>> {
    let (status, body) = call_bytes(
        crate::tokens::OP_TOKEN_PARSE_VERIFY,
        serde_json::json!({ "token": base64::encode(token), "root": root })
            .to_string()
            .as_bytes(),
    );
    let v = body?;
    (status == STATUS_OK).then(|| base64::decode(v["token"].as_str().unwrap()).unwrap())
}

/// Exercises the FFI panic boundary: a Rust panic is caught, never unwinds
/// across the ABI, reports `STATUS_PANIC`, and leaves the pre-initialized
/// empty buffer (no allocation after unwind is attempted).
#[test]
fn panic_is_contained_with_empty_output() {
    let (status, body) = call_bytes(crate::OP_TEST_PANIC, b"");
    assert_eq!(status, STATUS_PANIC, "panic must surface as STATUS_PANIC");
    assert!(body.is_none(), "panic path must not emit a body");
}

#[test]
fn deterministic_mutation_matrix() {
    let corpus = corpus();
    let mut rng = Rng(0x1B15_C911_5007_9E37);
    let mut rejected = 0u32;
    let mut proved = 0u32;
    let mut panics = 0u32;
    let mut allows = 0u32;
    let mut denies = 0u32;

    // A. Mutated tokens: fail closed, or normalize to canonical bytes that
    // re-verify. Framing-tolerant mutants (trailing bytes) are the only
    // legitimate successes.
    for _ in 0..2048 {
        let (mutant, strategy) = mutate_traced(&mut rng, &corpus.token);
        let (status, body) = call_bytes(
            crate::tokens::OP_TOKEN_PARSE_VERIFY,
            serde_json::json!({
                "token": base64::encode(&mutant),
                "root": corpus.root,
            })
            .to_string()
            .as_bytes(),
        );
        let Some(v) = body else {
            panics += 1;
            rejected += 1;
            continue;
        };
        if status != STATUS_OK {
            rejected += 1;
            continue;
        }
        let canonical = base64::decode(v["token"].as_str().unwrap()).unwrap();
        assert!(
            parses(&canonical, &corpus.root).is_some(),
            "canonical output must re-verify (strategy {strategy})"
        );
        if canonical != corpus.token {
            println!("note: strategy {strategy} verified with non-identical canonical bytes");
        }
        proved += 1;
    }

    // B. Mutated tokens authorize only with byte-identical canonical content.
    for _ in 0..1024 {
        let (mutant, strategy) = mutate_traced(&mut rng, &corpus.token);
        let (status, body) = call_bytes(
            crate::authorizer::OP_TOKEN_AUTHORIZE,
            allow_request(&mutant, &corpus.root).to_string().as_bytes(),
        );
        let Some(v) = body else {
            panics += 1;
            rejected += 1;
            continue;
        };
        if status != STATUS_OK {
            rejected += 1;
            continue;
        }
        if v["decision"] != "allow" {
            denies += 1;
            continue;
        }
        let canonical = parses(&mutant, &corpus.root).expect("authorized mutant must verify");
        assert_eq!(
            canonical, corpus.token,
            "authorized mutant must normalize to the corpus token (strategy {strategy})"
        );
        allows += 1;
    }

    // C. Attenuation: fail closed, or yield a verifiable child.
    for i in 0..256 {
        if i % 2 == 0 {
            let (mutant, _) = mutate_traced(&mut rng, &corpus.token);
            let (status, body) = call_bytes(
                crate::tokens::OP_TOKEN_ATTENUATE,
                serde_json::json!({
                    "token": base64::encode(&mutant),
                    "root": corpus.root,
                    "block": { "source": "check if operation(\"read\");" },
                })
                .to_string()
                .as_bytes(),
            );
            let Some(v) = body else {
                panics += 1;
                rejected += 1;
                continue;
            };
            if status != STATUS_OK {
                rejected += 1;
                continue;
            }
            let child = base64::decode(v["token"].as_str().unwrap()).unwrap();
            assert!(
                parses(&child, &corpus.root).is_some(),
                "attenuation output must verify"
            );
            proved += 1;
        } else {
            // Hand-built envelopes, including invalid UTF-8 sources. Only the
            // invalid-UTF-8 class is guaranteed to fail; a mutated source may
            // stay valid Datalog and then must still yield a verifiable child.
            let kind = rng.below(3);
            let mut envelope = format!(
                "{{\"token\":\"{}\",\"root\":{},\"block\":{{\"source\":\"",
                base64::encode(&corpus.token),
                corpus.root,
            )
            .into_bytes();
            let mut source = b"check if operation(\"read\");".to_vec();
            match kind {
                0 => source = mutate_bytes(&mut rng, &source),
                1 => source.extend_from_slice(&[0xFF, 0xFE]),
                _ => source.truncate(rng.below(source.len())),
            }
            envelope.extend_from_slice(&source);
            envelope.extend_from_slice(b"\"}}");
            let (status, body) = call_bytes(crate::tokens::OP_TOKEN_ATTENUATE, &envelope);
            let Some(v) = body else {
                panics += 1;
                rejected += 1;
                continue;
            };
            if kind == 1 {
                assert_ne!(status, STATUS_OK, "invalid UTF-8 envelope succeeded");
                rejected += 1;
            } else if status != STATUS_OK {
                rejected += 1;
            } else {
                let child = base64::decode(v["token"].as_str().unwrap()).unwrap();
                assert!(
                    parses(&child, &corpus.root).is_some(),
                    "attenuation output must verify"
                );
                proved += 1;
            }
        }
    }

    // D. Key imports: fail closed, or yield a usable, destroyable handle.
    // (Seed-region mutations of DER produce a different valid key.)
    for i in 0..256 {
        let (status, body) = match i % 3 {
            0 => {
                let mutant = mutate_bytes(&mut rng, &corpus.der);
                call_bytes(
                    crate::keys::OP_KEY_IMPORT,
                    serde_json::json!({
                        "encoding": "der",
                        "key": base64::encode(&mutant),
                    })
                    .to_string()
                    .as_bytes(),
                )
            }
            1 => {
                let mutant = mutate_bytes(&mut rng, corpus.pem.as_bytes());
                // Lossy transport: invalid UTF-8 becomes replacement chars, which
                // must still fail (as undecodable PEM), never succeed.
                call_bytes(
                    crate::keys::OP_KEY_IMPORT,
                    serde_json::json!({
                        "encoding": "pem",
                        "key": String::from_utf8_lossy(&mutant),
                    })
                    .to_string()
                    .as_bytes(),
                )
            }
            _ => {
                // Envelope confusion: wrong shapes and unknown param types.
                let bodies = [
                    serde_json::json!({ "encoding": "raw", "key": base64::encode(&corpus.der) }),
                    serde_json::json!({ "encoding": "der" }),
                    serde_json::json!({ "encoding": "der", "key": 42 }),
                    serde_json::json!({ "encoding": null, "key": null }),
                ];
                call_bytes(
                    crate::keys::OP_KEY_IMPORT,
                    bodies[rng.below(bodies.len())].to_string().as_bytes(),
                )
            }
        };
        let Some(v) = body else {
            panics += 1;
            rejected += 1;
            continue;
        };
        if status != STATUS_OK {
            rejected += 1;
            continue;
        }
        let handle = v["handle"].as_u64().unwrap();
        let (estatus, ebody) = call_bytes(
            crate::keys::OP_KEY_EXPORT_PUBLIC,
            serde_json::json!({ "handle": handle })
                .to_string()
                .as_bytes(),
        );
        assert!(
            ebody.is_some() && estatus == STATUS_OK,
            "imported key must be usable"
        );
        let (dstatus, dbody) = call_bytes(
            crate::keys::OP_KEY_DESTROY,
            serde_json::json!({ "handle": handle })
                .to_string()
                .as_bytes(),
        );
        assert!(
            dbody.is_some() && dstatus == STATUS_OK,
            "imported key must be destroyable"
        );
        proved += 1;
    }

    // E. Envelope abuse must never succeed (512 cases): unknown op ids,
    // null-output safety, and structurally confused requests are all
    // deterministically invalid.
    let confusing: Vec<serde_json::Value> = vec![
        serde_json::json!({ "root_handle": "not-a-number", "facts": [] }),
        serde_json::json!({ "root_handle": 0, "facts": [] }),
        serde_json::json!({ "root_handle": 1.5, "facts": [] }),
        serde_json::json!({ "root_handle": null }),
        serde_json::json!({ "facts": "not-an-array" }),
        serde_json::json!({ "facts": [{ "source": 42 }] }),
        serde_json::json!({ "facts": [{
            "source": "right({x}, \"read\")",
            "params": { "x": { "type": "float", "value": 1.5 } },
        }] }),
        serde_json::json!({ "facts": [{
            "source": "right({x}, \"read\")",
            "params": { "x": { "type": "int", "value": 9223372036854775808u64 } },
        }] }),
        serde_json::json!({ "facts": [{
            "source": "right({x}, \"read\")",
            "params": { "x": { "type": "bytes", "value": "!!!" } },
        }] }),
        serde_json::json!({ "token": 42, "root": corpus.root }),
        serde_json::json!({ "token": base64::encode(&corpus.token), "root": null }),
        serde_json::json!({ "token": base64::encode(&corpus.token) }),
    ];
    for i in 0..512 {
        let (status, body) = match i % 4 {
            0 => {
                // Unknown operation ids (7 is reserved-unimplemented), including
                // extremes. The implemented op 14 receiving empty input must
                // reject it, not crash.
                let ops = [7u32, 14, 100, 999, u32::MAX];
                let mut out = crate::BiscuitSharpBuffer {
                    data: ptr::null_mut(),
                    len: 0,
                };
                // SAFETY: null input with length zero is valid; output is writable.
                let status = unsafe {
                    crate::biscuitsharp_call_v1(ops[rng.below(ops.len())], ptr::null(), 0, &mut out)
                };
                let body = if out.data.is_null() || out.len == 0 {
                    None
                } else {
                    let slice =
                        unsafe { std::slice::from_raw_parts(out.data as *const u8, out.len) };
                    Some(serde_json::from_slice(slice).expect("valid JSON"))
                };
                crate::biscuitsharp_free_v1(out);
                (status, body)
            }
            1 => {
                // Null output must be safe, never a crash.
                // SAFETY: null input with length zero is valid; the null output
                // address is the documented invalid-input probe being tested.
                let status = unsafe {
                    crate::biscuitsharp_call_v1(
                        crate::tokens::OP_TOKEN_PARSE_VERIFY,
                        ptr::null(),
                        0,
                        ptr::null_mut(),
                    )
                };
                assert_eq!(status, STATUS_INVALID_INPUT);
                (status, None)
            }
            2 => call_bytes(
                crate::tokens::OP_TOKEN_CREATE,
                confusing[rng.below(confusing.len())].to_string().as_bytes(),
            ),
            _ => call_bytes(crate::tokens::OP_TOKEN_CREATE, b"{oops"),
        };
        // Arm 1 intentionally yields no body (null output address).
        if i % 4 == 1 {
            rejected += 1;
            continue;
        }
        if body.is_none() {
            panics += 1;
            rejected += 1;
            continue;
        }
        if status == STATUS_OK {
            panic!("envelope abuse succeeded");
        }
        if status == STATUS_PANIC {
            panics += 1;
        }
        rejected += 1;
    }

    println!("mutation matrix: rejected={rejected} proved={proved} denies={denies} allows={allows} panics={panics}");
    assert_eq!(panics, 0, "a panic crossed the ABI");
    assert_eq!(rejected + proved + denies + allows, 4096);
}

/// The standalone example runs this same workload without libtest allocations.
#[path = "../tests/support/leak_workload.rs"]
mod leak_workload;

#[test]
fn leak_probe_cycles() {
    leak_workload::run(call);
}
