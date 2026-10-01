# Review findings (open items)

Third review pass, 2026-10-01, updated 2026-10-02. Issues found and their
status. Items marked **Fixed** were resolved after the pass; the rest remain
honest follow-on work. The implemented surface has no known correctness
defects. Status labels: **Gap** (missing capability), **Limit** (by-design or
inherent, documented), **Deferred** (planned, spec §13/14/M2),
**Cosmetic** (style/process).

## Functional gaps

1. **Fixed 2026-10-02 — public-key import/parse.** New `key_import_public`
   bridge op (op 14) plus `BiscuitPublicKey.Parse`, validating raw public bytes
   for an algorithm without the private half. Upstream decode is lenient about
   non-canonical encodings (pinned by test); bogus keys still fail closed at
   verification time. Textual encodings (hex, prefixed strings, DER/PEM
   *public* keys) remain open.
2. **Fixed 2026-10-02 — authorizer rules.** `AddRule` on `BiscuitTokenBuilder`
   and `BiscuitAuthorizer`, wired through `code()` with malformed-rule tests.
3. **Partially fixed 2026-10-02 — loader verification.** Load-time manifest
   verification (hash + live identity, tamper-refusing, child-process probes)
   is implemented for staged/packaged assets; assets without an adjacent
   manifest keep ABI-check-only behavior. Full M2 manifest coverage (all RIDs)
   stays open.
4. **Partially done 2026-10-02 — NativeAOT, packaging, clean consumers.**
   win-x64 only, local: NativeAOT publish + execute green, single-RID pack +
   archive verification green, one isolated-cache clean consumer green. The
   three-RID CI matrix, `Verify-NativeStaging` / `Verify-NuGetPackage` /
   `Test-PackagedConsumer` automation, and publication stay open (M2).
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

24. **Fixed 2026-10-02 — clippy correctness.** `biscuitsharp_call_v1` is now
    `unsafe extern "C"` with documented safety; all internal call sites use
    explicit `unsafe` blocks. `cargo clippy --locked --all-targets -- -D warnings`
    is clean.
25. **Fixed 2026-10-02 — clippy style.** `tokens.rs` uses `?`; the fuzz splice
    loop uses iterators. `cargo fmt --check` is clean.
26. **Process — C# XML docs** are suppressed via `NoWarn` (CS1591); the plan is
    to require them before 1.0.
27. **Partially fixed 2026-10-02 — CI lint jobs.** A `native-lints` job runs
    `cargo fmt --check` and `cargo clippy -D warnings` (Ubuntu). rustfmt
    compliance of future edits is enforced; reviewers should still eyeball
    formatting in PRs since `fmt` cannot judge naming or structure.

## CI / process gaps

28. **Gap — native tests only on Windows.** The Linux/macOS managed jobs build
    the cdylib but do not run `cargo test`; the mutation matrix and differential
    tests therefore never run on those RIDs in CI.
29. **Fixed 2026-10-02 — `samples/BiscuitSharp.AotSmoke` is in
    `BiscuitSharp.slnx`.** Solution builds compile it; the win-x64 publish +
    execute smoke passed locally. Per-RID CI execution stays open (M2).
30. **Process — package-validation baseline removed** (a non-published baseline
    breaks `dotnet pack`); restore `PackageValidationBaselineVersion` once
    `0.1.0-preview.1` is on NuGet.

## Suggested order of follow-on work (updated 2026-10-02)

1. ~~Public-key import/parse~~ done; textual public-key encodings remain.
2. ~~`AddRule`~~ done.
3. M2 Linux/macOS: release builds, staging, CI matrix, remaining five clean
   consumers, `Verify-NativeStaging` / `Verify-NuGetPackage` /
   `Test-PackagedConsumer` automation, publication.
4. Linux/macOS native test jobs (Gap 8/28) and Valgrind (Gap 5).
5. Cancellation/limits (Gap 6); then clippy/rustfmt in CI (27).
