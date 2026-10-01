# Review findings (open items)

Third review pass, 2026-10-01, updated 2026-10-02. Issues found and their
status. Items marked **Fixed** were resolved after the pass; the rest remain
honest follow-on work. The audit identified correctness and documentation
defects; status below records their disposition. Status labels: **Gap** (missing capability), **Limit** (by-design or
inherent, documented), **Deferred** (planned, spec §13/14/M2),
**Cosmetic** (style/process).

## Audit disposition

F07 is addressed across the API contract, architecture, security guidance,
README, roadmap, changelog, and verification ledger. ADR 0002 records the wrapper
budget and normalization behavior; maintainer/team acceptance remains pending.
Prior CI is identified by SHA in the verification ledger, and its Valgrind
result is explicitly invalidated because the command selected zero tests.

## Functional gaps

1. **Fixed 2026-10-02 — public-key import/parse.** New `key_import_public`
   bridge op (op 14) plus `BiscuitPublicKey.Parse`, validating raw public bytes
   for an algorithm without the private half, plus `ParseHex`/`ParsePrefixed`
   and `ToPrefixedString()` for the upstream `ed25519/<hex>` display form.
   Upstream decode is lenient about non-canonical encodings (pinned by test);
   bogus keys still fail closed at verification time. DER/PEM *public* keys
   remain open.
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
5. **Open — corrected Valgrind run required.** The prior Linux CI command used
   `leak_probe --exact`, which selected zero tests (`0 passed, 45 filtered out`).
   The existing `leak_probe_cycles` workload covers 50 full-lifecycle cycles,
   but CI must select that exact test and prove it ran. Prior green CI does not
   establish leak coverage; see [verification](verification.md).
6. **Fixed 2026-10-02 — execution limits.** `BiscuitAuthorizerLimits` plus
   `WithLimits`, wired to upstream `set_limits`/`authorize_with_limits`.
   Finding: upstream's `RunLimits::default()` is `max_time: 1 ms`, which made
   the default path deny with `evaluation_failure` under CI load; BiscuitSharp
   now applies a robust default (100k facts / 100k iterations / 5 s) and
   exposes `UpstreamDefault` for strict parity. Cancellation of a running call
   remains unavailable (synchronous FFI).
7. **Gap — loader failure tests not written** for tampered/wrong-ABI/wrong-version
   assets (some depend on the M2 manifest). Current coverage: missing asset and
   relative override only.
8. **Partially fixed 2026-10-02 — mutation matrix runs per-OS in CI now**
   (`compat` job on Windows/Linux/macOS), but stays dev-profile. A
   release-profile run (where the shipped artifact's overflow behavior differs)
   remains open with M2.

## Known limitations (by design / inherent)

9. **Limit — `Import` rejects raw fixed-size secrets.** Raw 32-byte keys are
   ambiguous between Ed25519 and P-256, so only PKCS#8 PEM/DER (self-describing)
   are accepted; upstream *does* support `from_bytes(bytes, algorithm)`. A
   raw-key overload is possible if a caller needs it.
10. **Documented behavior — parse normalizes tolerated trailing framing bytes.**
    Upstream may accept such input; serialization returns canonical upstream
    bytes and drops the suffix. The API contract distinguishes raw transport
    bytes from parsed token identity; regression tests verify normalized bytes
    and equivalent authorization/revocation behavior. Team sign-off remains
    pending per ADR 0002.
11. **Documented behavior — tokens use fresh chain keys.** Each `Build()` mints
    fresh ephemeral chain keys, so equal-behavior tokens differ in serialized
    bytes and revocation IDs. Compare behavior, not bytes or IDs, across builds.
12. **Limit — `AddTimeFact` truncates to whole seconds.** Sub-second precision is
    dropped; fine for expiry-style checks, lossy for anything finer.
13. **Fixed — byte-array identity is protected.** Constructors copy input bytes
    and public `Value` / `Encoded` access returns defensive copies. Internal
    operations retain owned state without exposing mutable identity.
14. **Fixed — result and inspection collections are snapshots.** Constructor
    collections are copied and exposed collections are read-only snapshots;
    assigning a collection also snapshots it. Structural equality remains
    reference-based for collection members and is not promised.
15. **Limit — inspection reports the root algorithm as the signature algorithm.**
    Internal chain keys follow the root, so this is accurate for the token, but
    there is no separate per-block algorithm surface.
16. **Limit — public keys have no DER/PEM form.** Raw bytes, hex, and the
    `ed25519/<hex>` display form are covered; DER/PEM *public*-key encodings
    are not.
17. **Limit — the native key store is process-global and unbounded.** Handles are
    freed on `Dispose`/finalization; a caller that never disposes grows the store
    until the process ends. No cap or eviction.
18. **Fixed — native file hash is captured at load time.** `GetVersion()` uses
    the verified loader state's cached hash and does not re-read the binary on
    each call.
19. **Limit — builder/authorizer mutation is not thread-safe.** Concurrent
    mutation is a caller error; configured authorizers support concurrent
    `Authorize()` calls while unchanged, as documented in the API contract.
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
31. **Consistency — no parameterized authorizer facts/rules or block params.**
    The bridge supports `params` on every `code()` call, but only
    `BiscuitTokenBuilder.AddFact` exposes them; authorizer facts/rules/checks
    and `BiscuitBlock` are source-only. Harmless (textual Datalog covers all
    cases) but asymmetric; add if Hufu templating needs it.
32. **Gap — no builder `root_key_id`.** Upstream supports tagging the authority
    block with a root key id (reported back by inspection, usually null).
    Unexposed; add if multi-root deployments need it.

## Suggested order of follow-on work (updated 2026-10-02)

1. ~~Public-key import/parse~~ done (raw/hex/prefixed); DER/PEM public-key
   encodings remain.
2. ~~`AddRule`~~ done.
3. M2 Linux/macOS: release builds, staging, CI matrix, remaining five clean
   consumers, `Verify-NativeStaging` / `Verify-NuGetPackage` /
   `Test-PackagedConsumer` automation, publication.
4. Linux/macOS native test jobs (Gap 8/28) and Valgrind (Gap 5).
5. Cancellation of a running authorization call remains unavailable
   (synchronous FFI).
