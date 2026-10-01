//! Deterministic interop fixtures from a fixed seed, using direct upstream
//! calls only (no bridge).
//!
//! Seed-derived keys are byte-deterministic and pinned exactly. Token bytes are
//! NOT byte-stable across runs by design: issuance generates a fresh ephemeral
//! next-key per token (`BiscuitBuilder::build` → `build_with_rng`), so every
//! token (and its revocation IDs) is unique. `fixtures_match_committed`
//! therefore verifies the committed token semantically (parse, sources,
//! authorize) instead of comparing bytes. The ignored
//! `regenerate_committed_fixtures` rewrites the files:
//! `cargo test --locked --test committed_fixtures -- --ignored`.

use biscuit_auth::{Algorithm, AuthorizerBuilder, Biscuit, KeyPair, PrivateKey};
use std::path::PathBuf;

/// Fixed test seed (no security properties claimed; determinism is the point).
const SEED: [u8; 32] = [
    0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10,
    0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20,
];

const FACTS: [&str; 2] = [
    "right(\"workspace.main\", \"read\")",
    "perms(\"repo\", {1, 2})",
];

fn fixtures_dir() -> PathBuf {
    let mut dir = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
    dir.push("..");
    dir.push("fixtures");
    dir.push("compat");
    dir
}

/// Builds the deterministic fixture set, self-checked with a direct-Rust
/// authorization baseline.
fn build_fixtures() -> serde_json::Value {
    let root = KeyPair::from(&PrivateKey::from_bytes(&SEED, Algorithm::Ed25519).expect("seed"));
    let private_der = root.to_private_key_der().expect("der").to_vec();
    let public = root.public();
    let mut builder = Biscuit::builder();
    for fact in &FACTS {
        builder = builder.code(fact).expect("fact parses");
    }
    let token = builder
        .build(&root)
        .expect("build")
        .to_vec()
        .expect("serialize");
    let verified = Biscuit::from(&token, public).expect("verify");
    let mut authorizer = AuthorizerBuilder::new()
        .code("operation(\"read\")")
        .expect("fact")
        .code("allow if right(\"workspace.main\", \"read\");")
        .expect("policy")
        .build(&verified)
        .expect("build authorizer");
    assert!(authorizer.authorize().is_ok(), "direct baseline allows");

    serde_json::json!({
        "algorithm": "ed25519",
        "seed_hex": SEED.iter().map(|b| format!("{b:02x}")).collect::<String>(),
        "private_der_b64": base64::encode(&private_der),
        "public_b64": base64::encode(public.to_bytes()),
        "token_b64": base64::encode(&token),
        "facts": FACTS,
    })
}

#[test]
fn fixtures_match_committed() {
    let generated = build_fixtures();
    let path = fixtures_dir().join("genesis.json");
    let committed: serde_json::Value = serde_json::from_slice(
        &std::fs::read(&path)
            .unwrap_or_else(|_| panic!("committed fixture absent: {}", path.display())),
    )
    .expect("fixture JSON");
    // Seed-derived keys are byte-deterministic; pin them exactly.
    for field in ["algorithm", "seed_hex", "private_der_b64", "public_b64"] {
        assert_eq!(
            generated[field], committed[field],
            "committed fixture field '{field}' matches regeneration"
        );
    }
    // Token bytes are NOT byte-stable across runs by design (see above), so the
    // committed token is verified semantically instead of compared.
    let root = KeyPair::from(
        &PrivateKey::from_bytes(&SEED, Algorithm::Ed25519).expect("seed"),
    );
    let token_bytes =
        base64::decode(committed["token_b64"].as_str().expect("token")).expect("base64");
    let token = Biscuit::from(&token_bytes, root.public()).expect("committed token verifies");
    assert_eq!(token.block_count(), 1);
    let committed_sources = committed_token_sources(&token_bytes);
    for fact in &FACTS {
        // Sources print with normalized quoting; compare the predicate cores.
        let core = fact.split('(').next().expect("predicate");
        assert!(
            committed_sources[0].contains(core),
            "committed block prints '{core}'"
        );
    }
    let mut authorizer = AuthorizerBuilder::new()
        .code("operation(\"read\")")
        .expect("fact")
        .code("allow if right(\"workspace.main\", \"read\");")
        .expect("policy")
        .build(&token)
        .expect("build authorizer");
    assert!(authorizer.authorize().is_ok(), "committed token authorizes");
}

/// Printed block sources for structural comparison.
fn committed_token_sources(token: &[u8]) -> Vec<String> {
    let unverified =
        biscuit_auth::UnverifiedBiscuit::from(token).expect("committed token parses");
    (0..unverified.block_count())
        .map(|i| unverified.print_block_source(i).expect("block source"))
        .collect()
}

#[test]
#[ignore]
fn regenerate_committed_fixtures() {
    let dir = fixtures_dir();
    std::fs::create_dir_all(&dir).expect("fixture dir");
    std::fs::write(
        dir.join("genesis.json"),
        serde_json::to_string_pretty(&build_fixtures()).expect("json"),
    )
    .expect("write fixture");
}
