# Review findings (open items)

Third review pass, 2026-10-01. Issues **found and not fixed**, kept honest for
follow-on work. The implemented surface (keys, tokens, authorization) has no
known correctness defects; everything below is a gap, a deliberate limitation,
or a process item. Status labels: **Gap** (missing capability), **Limit**
(by-design or inherent, documented), **Deferred** (planned, spec §13/14/M2),
**Cosmetic** (style/process).

## Functional gaps

1. **Gap — no public-key import/parse.** `BiscuitPublicKey` can only be built
   from `BiscuitPrivateKey.PublicKey` or its raw-bytes constructor. There is no
   `Parse`/`Import` for upstream encoded forms (hex, `ed25519:`/`secp256r1:`
   prefixed strings, DER/PEM public keys) and no native import-public operation.
   A verifier that holds only a root public key (the common Hufu case) must
   obtain the exact raw bytes and algorithm out of band. Highest-value gap.
2. **Gap — no authorizer rules.** `BiscuitAuthorizer` exposes facts, checks, and
   policies (`AddFact`/`AddCheck`/`AddPolicy`/`AddTimeFact`) but not Datalog
   rules. Rules are part of the language; upstream `code()` already accepts
   them, so this is a one-method addition.
3. **Gap — loader verifies existence + ABI only.** Manifest, SHA-256, upstream
   version/commit, and lockfile identity are checked at *build/staging* time
   (`eng/` stubs) and reported by `GetVersion()`, but the loader does not verify
   them at load. Planned for M2 staging.
4. **Deferred — NativeAOT, packaging, clean consumers.** No AOT publish/execute,
   no `dotnet pack` verification, no external package consumers. M2.
5. **Deferred — leak instrumentation.** No Valgrind/equivalent run (needs Linux).
6. **Gap — no cancellation/timeout/tuning.** Authorization, issuance, and
   parsing run synchronously with upstream default execution limits and no way
   to set them or cancel. A hostile Datalog workload's ceiling is upstream's.
7. **Gap — loader failure tests not written** for tampered/wrong-ABI/wrong-version
   assets (some depend on the M2 manifest). Current coverage: missing asset and
   relative override only.
8. **Gap — mutation matrix is dev-profile and Windows-only.** It runs inside
   `cargo test --lib`, which only the Windows compat job executes; release-profile
   and Linux/macOS runs (where the shipped artifact's overflow behavior differs)
   remain open. M2.

## Known limitations (by design / inherent)

9. **Limit — `Import` rejects raw fixed-size secrets.** Raw 32-byte keys are
   ambiguous between Ed25519 and P-256, so only PKCS#8 PEM/DER (self-describing)
   are accepted; upstream *does* support `from_bytes(bytes, algorithm)`. A
   raw-key overload is possible if a caller needs it.
10. **Limit — `Parse` normalizes; `ToBytes()` may differ from the input.**
    Upstream framing tolerates trailing bytes, so a token with junk appended
    verifies and re-serializes to canonical bytes (the junk is dropped). Not yet
    stated in `docs/api-contract.md`.
11. **Limit — tokens are never byte-stable across issuances.** Each `Build()`
    mints a fresh ephemeral chain key, so equal-behavior tokens differ in bytes
    and revocation IDs. Compare behavior, not bytes. (Documented.)
12. **Limit — `AddTimeFact` truncates to whole seconds.** Sub-second precision is
    dropped; fine for expiry-style checks, lossy for anything finer.
13. **Limit — `BiscuitRevocationId.Value` / `BiscuitPublicKey.Encoded` return the
    stored arrays.** Inputs are defensively copied on construction, but mutating
    the returned array corrupts equality/hash codes. Returning copies (or
    `ReadOnlyMemory<byte>`) would be safer; deferred to avoid per-access cost.
14. **Limit — collection members use reference equality.** `BiscuitInspection`
    and `BiscuitAuthorizationResult` compare their list members by reference, so
    two structurally identical results are unequal. Return values only; not
    expected to be keyed.
15. **Limit — inspection reports the root algorithm as the signature algorithm.**
    Internal chain keys follow the root, so this is accurate for the token, but
    there is no separate per-block algorithm surface.
16. **Limit — public keys have no textual form.** `BiscuitPublicKey` is raw bytes
    + algorithm only (see Gap 1).
17. **Limit — the native key store is process-global and unbounded.** Handles are
    freed on `Dispose`/finalization; a caller that never disposes grows the store
    until the process ends. No cap or eviction.
18. **Limit — `GetVersion()` re-reads and SHA-256s the native binary every call.**
    Accurate by construction (reports the loaded file) but does file I/O per
    call; no caching.
19. **Limit — builder/authorizer are single-threaded accumulators.** Documented,
    not enforced; concurrent mutation is a caller error.
20. **Gap — no token fingerprint helper** for safe logging. Spec §25 mentions
    "token equality/fingerprints if exposed"; only equality is exposed.

## Deferred capabilities (spec §13/14)

21. **Deferred — third-party blocks** (request/signature/append/trusted-key).
22. **Deferred — authorizer/token snapshots.**
23. **Limit — panic containment covers only unwinding.** `catch_unwind` cannot
    catch abort/OOM-abort/stack-overflow/memory faults (documented; not testable
    here). The panic path itself is now tested (status 3, empty body).

## Static-analysis findings (cosmetic / process)

24. **Cosmetic — clippy correctness:** `biscuitsharp_call_v1` dereferences a
    caller-provided raw pointer without being `unsafe`
    (`clippy::not_unsafe_ptr_arg_deref`, deny-by-default). Conventional for a C
    ABI; marking `unsafe extern "C"` would ripple through internal call sites.
    Clippy is not wired into CI.
25. **Cosmetic — clippy style:** `native/src/tokens.rs:134` match→`?`;
    `native/src/adversarial.rs:93` index loop → iterator.
26. **Process — C# XML docs** are suppressed via `NoWarn` (CS1591); the plan is
    to require them before 1.0.
27. **Process — no CI for clippy/rustfmt** for the native crate.

## CI / process gaps

28. **Gap — native tests only on Windows.** The Linux/macOS managed jobs build
    the cdylib but do not run `cargo test`; the mutation matrix and differential
    tests therefore never run on those RIDs in CI.
29. **Gap — `samples/BiscuitSharp.AotSmoke` is not in `BiscuitSharp.slnx`.** A
    solution build never compiles it, so AOT regressions surface only when the
    M2 AOT job runs.
30. **Process — package-validation baseline removed** (a non-published baseline
    breaks `dotnet pack`); restore `PackageValidationBaselineVersion` once
    `0.1.0-preview.1` is on NuGet.

## Suggested order of follow-on work

1. Public-key import/parse (Gap 1) — unblocks verification-only consumers.
2. `AddRule` (Gap 2) and the Datalog rule tests.
3. M2 win-x64 staging: release triple build, manifest/hash, load-time
   verification (Gap 3), AOT smoke (Gap 4/29), package + consumer verification.
4. Linux/macOS native test jobs (Gap 8/28) and Valgrind (Gap 5).
5. Cancellation/limits (Gap 6); then clippy/rustfmt in CI (27).
