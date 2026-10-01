# BiscuitSharp contributor guidance

Read README.md, ROADMAP.md, docs/implementation-plan.md, docs/architecture.md,
docs/native-boundary.md, docs/api-contract.md, docs/security.md, and ADR 0001
before implementation. The repository is scaffolding: no native behavior is
qualified. Consult docs/verification.md for executed evidence; do not mistake
planned gates for qualified packages.

Preserve upstream Biscuit semantics rather than reinterpreting them for Hufu.
Keep workflow authority, grant issuance policy, revocation storage, workflow
revisions, human approval, delegation policy, and Hufu envelopes outside this
library. Never convert parse, verification, native loading, serialization, or
transport failures into a successful authorization decision. A valid token is
not an authorized request; an ordinary Deny is a result, not a bridge failure.

Wrap the official Rust implementation; do not reimplement Biscuit cryptography,
token serialization, or Datalog semantics in C#. Pin the upstream source commit,
Rust toolchain, dependency lockfile, token/spec version, and native ABI.
Validate ownership, UTF-8 boundaries, lengths, error conversion, concurrency,
and supported runtime identifiers. Do not expose raw pointers to public callers.
Keep private key material in opaque native handles; explicit export is allowed
but normal issuance must not copy secrets through managed memory repeatedly.
`ToString()` must never reveal private material.

Reserve `BiscuitBridgeException` for native/ABI/transport failures; use
`BiscuitTokenException` (and `BiscuitSignatureException`/`BiscuitFormatException`/
`BiscuitSealedTokenException`) for token validity, `BiscuitDatalogException` for
Datalog failures, and `BiscuitAuthorizationException` for enforcement so a Deny
is never reported as a bridge failure.

Keep the library trim- and NativeAOT-analyzer clean. Prefer `JsonElement`,
`JsonNode`, and `JsonTypeInfo<T>` paths; annotate reflection-based convenience
overloads with `RequiresUnreferencedCode`/`RequiresDynamicCode`. Do not route
native loading through `Assembly.Location`. Tracing must never export private
keys, raw bearer tokens, or sensitive Datalog by default.

Add meaningful native differential (bridge vs direct Rust), bidirectional
compatibility, and packaged-consumer tests when behavior exists. Run only
supported platforms and report untested platforms honestly. Keep package
publication, remote repository changes, and Hufu integration separate from
implementing the wrapper unless the task authorizes them.
