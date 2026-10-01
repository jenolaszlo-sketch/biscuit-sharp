# Upstream upgrade procedure

How to move the pinned biscuit-auth baseline. Upgrades re-run the full gate;
a manifest change alone is never a qualification.

1. Update `native/Cargo.toml` to the exact new version (`=` pin), update
   `rust-toolchain.toml` if upstream MSRV/CI requires it, and run
   `cargo update -p biscuit-auth` equivalents with `--locked` discipline.
2. Commit the new `Cargo.lock`. Record the new source commit, crate checksums,
   token/spec version, and enabled features in `docs/native-boundary.md` and
   the staged `biscuitsharp-native.json` schema.
3. Refresh `native/legal/` licenses and `NOTICE` (upstream + transitive notices).
4. Update Datalog syntax notes (spec version changes alter text representation).
5. Re-run: native differential tests, bidirectional compatibility, managed suite
   on .NET 8/10, mutation/fuzz, leak instrumentation, NativeAOT per RID, package
   assembly + archive verification, six clean consumers.
6. Record old and new identity in `docs/verification.md` and `CHANGELOG.md` so
   consumers can audit historical decisions. Hufu determines when new semantics
   apply to admitted bundles.
