// Build script: emits bridge/upstream identity compiled into the cdylib and
// reported by the version operation (docs/native-boundary.md).
use std::env;
use std::fs;
use std::path::PathBuf;
use std::process::Command;

use sha2::Digest;

fn main() {
    println!("cargo:rustc-env=BISCUITSHARP_BRIDGE_VERSION=0.1.0");
    println!("cargo:rustc-env=BISCUITSHARP_ABI_VERSION=1");
    // biscuit-auth default features requested by [dependencies] below.
    // Verified against the biscuit-auth 6.0.0 manifest: default =
    // ["regex-full", "datalog-macro", "pem"]. Re-check on every upgrade.
    println!("cargo:rustc-env=BISCUITSHARP_ENABLED_FEATURES=regex-full,datalog-macro,pem");

    // Pinned toolchain identity, e.g. "1.89.0 (29483883e 2025-08-04)".
    let rust_version = Command::new("rustc")
        .arg("-vV")
        .output()
        .ok()
        .and_then(|o| String::from_utf8(o.stdout).ok())
        .and_then(|s| {
            s.lines()
                .next()
                .and_then(|l| l.strip_prefix("rustc "))
                .map(str::to_owned)
        })
        .unwrap_or_else(|| "unknown".to_owned());
    println!("cargo:rustc-env=BISCUITSHARP_RUST_VERSION={rust_version}");

    // biscuit-auth version + Cargo.lock SHA-256, read from the manifest dir so
    // the binary always reports the lockfile it was built with.
    let manifest_dir =
        PathBuf::from(env::var("CARGO_MANIFEST_DIR").expect("CARGO_MANIFEST_DIR is set"));
    let lock = fs::read(manifest_dir.join("Cargo.lock")).expect("Cargo.lock must be committed");
    let digest = sha2::Sha256::digest(&lock);
    println!("cargo:rustc-env=BISCUITSHARP_CARGO_LOCK_SHA256={digest:x}");

    let text = String::from_utf8_lossy(&lock);
    let version = parse_package_version(&text, "biscuit-auth").expect("biscuit-auth in Cargo.lock");
    println!("cargo:rustc-env=BISCUITSHARP_BISCUIT_AUTH_VERSION={version}");

    // Compile-time triple for the crate itself (TARGET is only set for build scripts).
    let target = env::var("TARGET").expect("TARGET is set for build scripts");
    println!("cargo:rustc-env=BISCUITSHARP_TARGET_TRIPLE={target}");

    println!("cargo:rerun-if-changed=src/lib.rs");
    println!("cargo:rerun-if-changed=Cargo.toml");
    println!("cargo:rerun-if-changed=Cargo.lock");
}

/// Minimal Cargo.lock parser: finds `name = "<name>"`, then the next `version`.
fn parse_package_version(lock: &str, name: &str) -> Option<String> {
    let header = format!("name = \"{name}\"");
    let mut lines = lock.lines();
    while let Some(line) = lines.next() {
        if line.trim() == header {
            for next in lines.by_ref() {
                let t = next.trim();
                if let Some(v) = t
                    .strip_prefix("version = \"")
                    .and_then(|s| s.strip_suffix('"'))
                {
                    return Some(v.to_owned());
                }
                if t.starts_with('[') || t.starts_with("dependencies") {
                    break;
                }
            }
        }
    }
    None
}
