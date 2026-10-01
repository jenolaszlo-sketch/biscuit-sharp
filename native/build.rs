// Build script: emits bridge/upstream identity for biscuitsharp-native.json staging (M2).
fn main() {
    println!("cargo:rustc-env=BISCUITSHARP_BRIDGE_VERSION=0.1.0");
    println!("cargo:rustc-env=BISCUITSHARP_ABI_VERSION=1");
    println!("cargo:rerun-if-changed=src/lib.rs");
    println!("cargo:rerun-if-changed=Cargo.toml");
    println!("cargo:rerun-if-changed=Cargo.lock");
}
