//! Generates Rust-origin compatibility fixtures with direct upstream calls only
//! (no bridge): a root key, a parent token, and a direct-authorize baseline.
//! Managed tests consume these (eng/Test-Compat.ps1), proving Rust-issued
//! tokens verify and authorize through BiscuitSharp.

use biscuit_auth::{AuthorizerBuilder, Biscuit, KeyPair};
use std::path::PathBuf;

fn out_dir() -> PathBuf {
    let mut dir = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
    dir.push("..");
    dir.push("artifacts");
    dir.push("compat");
    dir.push("rust");
    dir
}

fn write_json(name: &str, value: &serde_json::Value) {
    let dir = out_dir();
    std::fs::create_dir_all(&dir).expect("fixture dir");
    std::fs::write(
        dir.join(name),
        serde_json::to_string_pretty(value).expect("json"),
    )
    .expect("write fixture");
}

#[test]
fn generate_rust_fixtures() {
    let root = KeyPair::new_with_algorithm("ed25519".parse().unwrap());
    let private_der = root.to_private_key_der().expect("der").to_vec();
    let public = root.public();
    let facts = [
        "right(\"workspace.main\", \"read\")",
        "perms(\"repo\", {1, 2, 3})",
    ];
    let mut builder = Biscuit::builder();
    for fact in &facts {
        builder = builder.code(fact).expect("fact parses");
    }
    let token = builder
        .build(&root)
        .expect("build")
        .to_vec()
        .expect("serialize");
    // Direct-Rust baseline: the parent authorizes here, so the managed side
    // must reach the same decision through the bridge.
    let verified = Biscuit::from(&token, public).expect("verify");
    let mut authorizer = AuthorizerBuilder::new()
        .code("operation(\"read\")")
        .expect("fact")
        .code("allow if right(\"workspace.main\", \"read\");")
        .expect("policy")
        .build(&verified)
        .expect("build authorizer");
    assert!(authorizer.authorize().is_ok(), "direct baseline allows");

    write_json(
        "root.json",
        &serde_json::json!({
            "algorithm": "ed25519",
            "private_der_b64": base64::encode(&private_der),
            "public_b64": base64::encode(public.to_bytes()),
        }),
    );
    write_json(
        "parent.json",
        &serde_json::json!({
            "algorithm": "ed25519",
            "token_b64": base64::encode(&token),
            "facts": facts,
        }),
    );
}
