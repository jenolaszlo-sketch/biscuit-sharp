//! Consumes managed-origin fixtures with direct upstream calls only (no bridge):
//! verifies and authorizes the BiscuitSharp-issued token, verifies the
//! BiscuitSharp-attenuated child, then attenuates the managed token with
//! direct Rust for the managed consume phase. Run after the managed tests
//! (eng/Test-Compat.ps1); fails loudly when fixtures are absent.

use biscuit_auth::{Algorithm, AuthorizerBuilder, Biscuit, BlockBuilder, PublicKey};
use std::path::PathBuf;
use std::str::FromStr;

fn compat_dir() -> PathBuf {
    let mut dir = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
    dir.push("..");
    dir.push("artifacts");
    dir.push("compat");
    dir
}

fn read_json(dir: &std::path::Path, name: &str) -> serde_json::Value {
    let path = dir.join(name);
    let hint = if path.exists() {
        "unreadable"
    } else {
        "absent (run the managed tests first: eng/Test-Compat.ps1)"
    };
    let bytes =
        std::fs::read(&path).unwrap_or_else(|_| panic!("fixture {}: {}", path.display(), hint));
    serde_json::from_slice(&bytes).expect("fixture JSON")
}

fn parse_algorithm(name: &str) -> Algorithm {
    Algorithm::from_str(name).expect("known algorithm")
}

fn parse_public(value: &serde_json::Value) -> PublicKey {
    let algorithm = parse_algorithm(value["algorithm"].as_str().expect("algorithm"));
    let bytes = base64::decode(value["public_b64"].as_str().expect("public_b64")).expect("base64");
    PublicKey::from_bytes(&bytes, algorithm).expect("public key")
}

fn authorize_allow(token: &Biscuit, operation: &str) {
    let mut authorizer = AuthorizerBuilder::new()
        .code(format!("operation(\"{operation}\")"))
        .expect("fact")
        .code("allow if right(\"workspace.main\", \"read\");")
        .expect("policy")
        .build(token)
        .expect("build authorizer");
    assert!(
        authorizer.authorize().is_ok(),
        "direct Rust allows {operation}"
    );
}

#[test]
fn consume_managed_fixtures() {
    let base = compat_dir();
    let rust_dir = base.join("rust");
    let managed_dir = base.join("managed");
    let rust_root = read_json(&rust_dir, "root.json");
    let managed_token = read_json(&managed_dir, "token.json");
    let managed_child = read_json(&managed_dir, "child.json");

    // Direction 2: the BiscuitSharp-issued token verifies with direct Rust.
    let managed_root = parse_public(&managed_token);
    let managed_bytes =
        base64::decode(managed_token["token_b64"].as_str().expect("token")).expect("base64");
    let token = Biscuit::from(&managed_bytes, managed_root).expect("managed token verifies");
    authorize_allow(&token, "read");

    // Direction 3: the BiscuitSharp-attenuated child of the Rust parent verifies
    // with direct Rust and still authorizes for the narrowed operation.
    let rust_public = parse_public(&rust_root);
    let child_bytes =
        base64::decode(managed_child["token_b64"].as_str().expect("token")).expect("base64");
    let child = Biscuit::from(&child_bytes, rust_public).expect("managed child verifies");
    assert_eq!(
        child.block_count(),
        2,
        "attenuation appended exactly one block"
    );
    authorize_allow(&child, "read");

    // Direction 4 setup: direct Rust attenuates the managed token; the managed
    // consume phase verifies and authorizes the result through the bridge.
    let child2 = token
        .append(
            BlockBuilder::new()
                .code("check if operation(\"read\");")
                .expect("check"),
        )
        .expect("direct attenuate")
        .to_vec()
        .expect("serialize");
    let child2_token = Biscuit::from(&child2, managed_root).expect("child2 verifies");
    authorize_allow(&child2_token, "read");
    std::fs::write(
        rust_dir.join("child2.json"),
        serde_json::to_string_pretty(&serde_json::json!({
            "token_b64": base64::encode(&child2),
        }))
        .expect("json"),
    )
    .expect("write child2");
}
