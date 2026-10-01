# Native bridge

Versioned Rust `cdylib` bridge over `biscuit-auth = "=6.0.0"` (ABI 1).

- Stable C ABI 1: `biscuitsharp_abi_version`, `biscuitsharp_call_v1`, `biscuitsharp_free_v1`.
- Pointer + length inputs, native-owned output buffers with exactly one free.
- Recoverable panics are caught; never unwind across FFI.
- Input limit 16 MiB, output limit 64 MiB (wire messages, not evaluation time).
- M0 pins the exact source commit, Rust toolchain, and `Cargo.lock`; M1
  operations (version, keys, tokens, authorization) are implemented with
  boundary, differential, compatibility, and mutation tests — see
  `docs/native-boundary.md` and `docs/implementation-plan.md`.

Build (once Rust is installed): `cargo build --locked --release`.

Prerequisites: the pinned toolchain (rustup auto-installs it from
`../rust-toolchain.toml`) and, on Windows, the MSVC linker — Visual Studio with
the "Desktop development with C++" workload. Without `link.exe`, dependency
resolution (`cargo fetch --locked`) still works but compiling the `cdylib` fails.
`Cargo.lock` is committed; never build with floating versions.
