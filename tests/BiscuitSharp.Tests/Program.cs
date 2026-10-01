// M1 test runner (no external test framework yet): version/loader + key slices
// against the real native asset. Requires a built bridge:
// `cargo build --locked` in native/.
// Full native/managed matrices live in docs/implementation-plan.md.
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BiscuitSharp;

if (args.Length == 2 && args[0] == "--manifest-probe")
{
    return ManifestProbe(args[1]);
}

int failures = 0;

void Check(bool condition, string name)
{
    Console.WriteLine($"{(condition ? "PASS" : "FAIL")}: {name}");
    if (!condition)
    {
        failures++;
    }
}

static bool Throws<TException>(Func<object?> action)
    where TException : Exception
{
    try
    {
        action();
        return false;
    }
    catch (TException)
    {
        return true;
    }
}

static bool IsHex64(string s) =>
    s.Length == 64 && s.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));

string? previousOverride = Environment.GetEnvironmentVariable("BISCUITSHARP_NATIVE_PATH");
try
{
    // 1. Loader fails closed on a missing asset. Must run FIRST: the loaded
    // handle is cached per process, so no successful load may precede this.
    Environment.SetEnvironmentVariable(
        "BISCUITSHARP_NATIVE_PATH", Path.Combine(Path.GetTempPath(), "biscuit-sharp-missing-native.dll"));
    Check(
        Throws<BiscuitBridgeException>(() => BiscuitEngine.GetVersion()),
        "missing native asset fails closed");

    // 2. Relative overrides resolve against the current directory (no load).
    Environment.SetEnvironmentVariable("BISCUITSHARP_NATIVE_PATH", "relative/bridge.dll");
    string resolved = NativeBridge.GetNativeAssetPath("win-x64");
    Check(
        Path.IsPathRooted(resolved)
            && resolved == Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "relative/bridge.dll")),
        "relative native override resolves against the current directory");

    // 3. Locate the real bridge built from native/ (override wins if it exists).
    string rid = NativeLoader.GetRuntimeIdentifier();
    string asset = FindNativeAsset(NativeBridge.GetNativeFileName(rid));
    Check(asset != "", $"native bridge asset present for {rid}");
    if (asset == "")
    {
        Console.WriteLine("Build the bridge first: `cargo build --locked` in native/.");
        return 1;
    }

    Environment.SetEnvironmentVariable("BISCUITSHARP_NATIVE_PATH", asset);

    // 4. Live version identity, differentially checked against the files.
    BiscuitSharpVersionInfo version = BiscuitEngine.GetVersion();
    Console.WriteLine(
        $"native: biscuit-auth={version.BiscuitAuthVersion} bridge={version.BridgeVersion} " +
        $"abi={version.AbiVersion} triple={version.TargetTriple} rid={version.RuntimeIdentifier}");
    Check(version.BiscuitAuthVersion == "6.0.0", "biscuit-auth version is 6.0.0");
    Check(version.AbiVersion == 1, "ABI version is 1");
    Check(BiscuitEngine.AbiVersion == NativeBridge.ExpectedAbiVersion, "managed ABI constants agree");
    Check(version.BridgeVersion == "0.1.0", "bridge version is 0.1.0");
    Check(version.RustVersion.StartsWith("1.89.0", StringComparison.Ordinal), "Rust version is pinned 1.89.0");
    Check(version.RuntimeIdentifier == rid, "runtime identifier matches host");
    Check(
        rid.StartsWith("win-", StringComparison.Ordinal) ? version.TargetTriple.Contains("windows", StringComparison.Ordinal) :
        rid.StartsWith("linux-", StringComparison.Ordinal) ? version.TargetTriple.Contains("linux", StringComparison.Ordinal) :
        version.TargetTriple.Contains("darwin", StringComparison.Ordinal),
        "target triple matches host RID");
    Check(
        version.UpstreamCommit == "0f0b4e0e6fe07220c1ba6b51bff21d450d94a975",
        "upstream commit matches the pinned tag");
    Check(version.TokenSpecVersion?.Contains("3.3", StringComparison.Ordinal) == true, "spec version reports Datalog 3.3");
    Check(version.EnabledFeatures.Split(',').Contains("pem"), "PEM feature reported enabled");
    Check(IsHex64(version.NativeSha256), "native SHA-256 is 64 hex chars");
    Check(IsHex64(version.CargoLockHash), "Cargo.lock hash is 64 hex chars");

    string expectedBinaryHash =
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(asset))).ToLowerInvariant();
    Check(version.NativeSha256 == expectedBinaryHash, "native SHA-256 matches the loaded file");

    string? lockPath = FindCargoLock(asset);
    Check(lockPath != null, "Cargo.lock located next to the bridge source");
    if (lockPath != null)
    {
        string expectedLockHash =
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(lockPath))).ToLowerInvariant();
        Check(version.CargoLockHash == expectedLockHash, "Cargo.lock hash matches the build input");
    }

    // 5. Keys: generate both algorithms over opaque native handles.
    using BiscuitPrivateKey edKey = BiscuitPrivateKey.Generate(BiscuitKeyAlgorithm.Ed25519);
    using BiscuitPrivateKey p256Key = BiscuitPrivateKey.Generate(BiscuitKeyAlgorithm.P256);
    Check(edKey.Algorithm == BiscuitKeyAlgorithm.Ed25519, "Ed25519 generate reports its algorithm");
    Check(p256Key.Algorithm == BiscuitKeyAlgorithm.P256, "P-256 generate reports its algorithm");
    Check(edKey.PublicKey.Encoded.Length == 32, "Ed25519 public key is 32 bytes");
    Check(p256Key.PublicKey.Encoded.Length > 0, "P-256 public key is non-empty");
    using BiscuitPrivateKey edKey2 = BiscuitPrivateKey.Generate();
    Check(!edKey.PublicKey.Encoded.SequenceEqual(edKey2.PublicKey.Encoded), "fresh keys differ");

    // 6. DER export/import round-trips preserve the public half and algorithm.
    foreach ((BiscuitPrivateKey key, string name) in new (BiscuitPrivateKey, string)[]
             {
                 (edKey, "Ed25519"),
                 (p256Key, "P-256"),
             })
    {
        byte[] exported = key.Export();
        Check(exported.Length > 0, $"{name} export is non-empty DER");
        using BiscuitPrivateKey imported = BiscuitPrivateKey.Import(exported);
        Check(imported.Algorithm == key.Algorithm, $"{name} import preserves the algorithm");
        Check(
            imported.PublicKey.Encoded.SequenceEqual(key.PublicKey.Encoded),
            $"{name} export/import round-trips the public half");
    }

    // 7. PEM import: armor the DER export exactly like upstream PKCS#8 PEM.
    byte[] der = edKey.Export();
    string pem = "-----BEGIN PRIVATE KEY-----\n" + ChunkBase64(der) + "-----END PRIVATE KEY-----\n";
    using BiscuitPrivateKey fromPem = BiscuitPrivateKey.Import(Encoding.ASCII.GetBytes(pem));
    Check(
        fromPem.Algorithm == BiscuitKeyAlgorithm.Ed25519
            && fromPem.PublicKey.Encoded.SequenceEqual(edKey.PublicKey.Encoded),
        "PEM import round-trips the public half");

    // 8. Malformed imports fail as key errors, never as success or bridge noise.
    Check(Throws<BiscuitKeyException>(() => BiscuitPrivateKey.Import(Array.Empty<byte>())), "empty import fails");
    Check(Throws<BiscuitKeyException>(() => BiscuitPrivateKey.Import(new byte[] { 1, 2, 3 })), "garbage import fails");
    Check(
        Throws<BiscuitKeyException>(() => BiscuitPrivateKey.Import(der[..Math.Max(0, der.Length - 10)])),
        "truncated DER import fails");
    byte[] corrupt = (byte[])der.Clone();
    corrupt[0] ^= 0xFF; // ASN.1 SEQUENCE tag: structurally invalid, deterministically rejected
    Check(Throws<BiscuitKeyException>(() => BiscuitPrivateKey.Import(corrupt)), "corrupt DER import fails");
    Check(
        Throws<ArgumentOutOfRangeException>(() => BiscuitPrivateKey.Generate((BiscuitKeyAlgorithm)42)),
        "unknown algorithm fails fast");

    // 9. Disposal: use-after-dispose throws, double dispose is safe.
    var doomed = BiscuitPrivateKey.Generate();
    doomed.Dispose();
    Check(Throws<ObjectDisposedException>(() => doomed.Export()), "use after dispose fails");
    doomed.Dispose();
    Check(true, "double dispose is safe");

    // 10. Privacy: ToString never carries key material.
    string keyText = edKey.ToString();
    Check(
        keyText.Contains("Ed25519", StringComparison.Ordinal)
            && !keyText.Contains(Convert.ToBase64String(edKey.PublicKey.Encoded), StringComparison.Ordinal),
        "ToString reveals no key material");

    // 11. Concurrency: parallel generate/export/import/dispose cycles.
    int keyErrors = 0;
    Parallel.For(0, 64, _ =>
    {
        try
        {
            using var k = BiscuitPrivateKey.Generate();
            using var i = BiscuitPrivateKey.Import(k.Export());
            if (!i.PublicKey.Encoded.SequenceEqual(k.PublicKey.Encoded))
            {
                Interlocked.Increment(ref keyErrors);
            }
        }
        catch
        {
            Interlocked.Increment(ref keyErrors);
        }
    });
    Check(keyErrors == 0, "concurrent generate/export/import/dispose");

    // 12. Tokens: issue with the specification's builder shape.
    using BiscuitPrivateKey rootKey = BiscuitPrivateKey.Generate();
    BiscuitToken token = BiscuitTokenBuilder
        .Create()
        .AddFact("""right("workspace.main", "read")""")
        .AddFact("""right("workspace.main", "write")""")
        .Build(rootKey);
    Check(token.ToBytes().Length > 0, "issued token is non-empty");
    Check(token.Root.Encoded.SequenceEqual(rootKey.PublicKey.Encoded), "token carries its root");

    // 13. Parse round-trips canonical bytes; base64url too.
    BiscuitToken parsed = BiscuitToken.Parse(token.ToBytes(), rootKey.PublicKey);
    Check(parsed == token, "parse round-trips canonical bytes");
    Check(parsed.GetHashCode() == token.GetHashCode(), "equal tokens hash equally");
    BiscuitToken fromUrl = BiscuitToken.ParseBase64Url(token.ToBase64Url(), rootKey.PublicKey);
    Check(fromUrl == token, "base64url round-trips");
    Check(Throws<BiscuitFormatException>(() => BiscuitToken.Parse(Array.Empty<byte>(), rootKey.PublicKey)), "empty parse fails");
    Check(Throws<BiscuitFormatException>(() => BiscuitToken.ParseBase64Url("!!!", rootKey.PublicKey)), "bad base64url fails");

    // 14. Verification failures are typed and never success.
    using BiscuitPrivateKey otherRoot = BiscuitPrivateKey.Generate();
    Check(Throws<BiscuitSignatureException>(() => BiscuitToken.Parse(token.ToBytes(), otherRoot.PublicKey)), "wrong root is a signature error");
    byte[] tampered = token.ToBytes();
    tampered[^1] ^= 0xFF;
    Check(Throws<BiscuitTokenException>(() => BiscuitToken.Parse(tampered, rootKey.PublicKey)), "tampered token fails");
    byte[] truncated = token.ToBytes()[..^20];
    Check(Throws<BiscuitFormatException>(() => BiscuitToken.Parse(truncated, rootKey.PublicKey)), "truncated token is a format error");
    Check(Throws<BiscuitFormatException>(() => BiscuitToken.Parse(new byte[] { 1, 2, 3 }, rootKey.PublicKey)), "garbage token is a format error");

    // 15. Attenuation appends a narrowing block.
    BiscuitToken child = token.Attenuate(BiscuitBlock.Create("""
        check if operation("read");
        """));
    Check(child != token, "attenuation produces a distinct token");
    Check(BiscuitToken.Parse(child.ToBytes(), rootKey.PublicKey) == child, "child verifies");
    BiscuitInspection childView = child.Inspect();
    Check(childView.BlockCount == 2, "child has two blocks");
    Check(childView.BlockSources[1].Contains("operation", StringComparison.Ordinal), "child carries the check");
    Check(Throws<BiscuitDatalogException>(() => token.Attenuate(BiscuitBlock.Create("check if"))), "malformed attenuation is a Datalog error");
    Check(Throws<ArgumentException>(() => BiscuitBlock.Create("  ")), "empty block source fails fast");

    // 16. Sealing forbids further attenuation.
    BiscuitToken sealedToken = token.Seal();
    Check(sealedToken.Inspect().IsSealed, "sealed token reports sealed");
    Check(!token.Inspect().IsSealed, "parent stays unsealed");
    Check(Throws<BiscuitSealedTokenException>(() => sealedToken.Attenuate(BiscuitBlock.Create("""check if operation("read");"""))), "append after seal fails");
    Check(Throws<BiscuitSealedTokenException>(() => sealedToken.Seal()), "seal after seal fails");

    // 17. Revocation IDs grow with blocks and stay opaque.
    IReadOnlyList<BiscuitRevocationId> parentIds = token.GetRevocationIds();
    Check(parentIds.Count == 1 && parentIds[0].Value.Length > 0, "one revocation id per block");
    IReadOnlyList<BiscuitRevocationId> childIds = child.GetRevocationIds();
    Check(childIds.Count == 2, "child has two revocation ids");
    Check(childIds.Any(id => id.Value.SequenceEqual(parentIds[0].Value)), "authority id persists");

    // 18. Inspection reports structural facts.
    BiscuitInspection view = token.Inspect();
    Check(view.BlockCount == 1, "one authority block");
    Check(!view.IsSealed, "fresh token is unsealed");
    Check(view.SignatureAlgorithm == BiscuitKeyAlgorithm.Ed25519 && view.RootKeyAlgorithm == BiscuitKeyAlgorithm.Ed25519, "algorithms match root");
    Check(view.TokenSizeBytes == token.ToBytes().Length, "token size matches");
    Check(view.BlockSources.Count == 1 && view.BlockSources[0].Contains("workspace.main", StringComparison.Ordinal), "block source is printed");
    Check(view.FormatVersion is not null, "format version reported");
    Check(view.RootKeyId is null, "no root key id set");

    // 19. Parameterized facts substitute without interpolation.
    BiscuitToken paramToken = BiscuitTokenBuilder
        .Create()
        .AddFact(
            "right({resource}, {operation})",
            new Dictionary<string, BiscuitParam>
            {
                ["resource"] = BiscuitParam.Str("workspace.main"),
                ["operation"] = BiscuitParam.Str("read"),
            })
#pragma warning disable IL2026, IL3050 // Intentional: exercises the reflection convenience overload.
        .AddFact(
            "right({resource}, {operation})",
            new { resource = "workspace.main", operation = "write" })
#pragma warning restore IL2026, IL3050
        .Build(rootKey);
    Check(paramToken.Inspect().BlockSources[0].Contains("workspace.main", StringComparison.Ordinal), "params substituted");
    Check(Throws<BiscuitDatalogException>(() => BiscuitTokenBuilder.Create().AddFact("right(").Build(rootKey)), "malformed fact is a Datalog error");
    Check(Throws<ArgumentException>(() => BiscuitTokenBuilder.Create().AddFact("")), "empty fact fails fast");

    // 20. P-256 tokens verify; unicode survives the round-trip.
    using BiscuitPrivateKey p256Root = BiscuitPrivateKey.Generate(BiscuitKeyAlgorithm.P256);
    BiscuitToken p256Token = BiscuitTokenBuilder.Create().AddFact("""right("a", "read")""").Build(p256Root);
    Check(BiscuitToken.Parse(p256Token.ToBytes(), p256Root.PublicKey) == p256Token, "P-256 token verifies");
    Check(p256Token.Inspect().SignatureAlgorithm == BiscuitKeyAlgorithm.P256, "P-256 algorithm reported");
    BiscuitToken uniToken = BiscuitTokenBuilder.Create().AddFact("""right("espace café ☕", "read")""").Build(rootKey);
    Check(uniToken.Inspect().BlockSources[0].Contains("café", StringComparison.Ordinal), "unicode survives");

    // 21. Token concurrency: parallel issue/parse/attenuate.
    int tokenErrors = 0;
    Parallel.For(0, 32, _ =>
    {
        try
        {
            using var k = BiscuitPrivateKey.Generate();
            var t = BiscuitTokenBuilder.Create().AddFact("""right("a", "read")""").Build(k);
            var c = t.Attenuate(BiscuitBlock.Create("""check if operation("read");"""));
            if (BiscuitToken.Parse(c.ToBytes(), k.PublicKey) != c || c.Inspect().BlockCount != 2)
            {
                Interlocked.Increment(ref tokenErrors);
            }
        }
        catch
        {
            Interlocked.Increment(ref tokenErrors);
        }
    });
    Check(tokenErrors == 0, "concurrent issue/parse/attenuate");

    // 22. Allow: the specification's end-to-end shape.
    BiscuitAuthorizationResult allowResult = BiscuitAuthorizer
        .For(token)
        .AddFact("""resource("/src/Foo.cs")""")
        .AddFact("""operation("read")""")
        .AddPolicy("""allow if right("workspace.main", "read");""")
        .Authorize();
    Check(allowResult.IsAuthorized, "matching request is authorized");
    Check(allowResult.Decision == BiscuitDecision.Allow && allowResult.Errors.Count == 0, "clean allow carries no errors");
    Check(allowResult.AllowPolicyIndex == 0, "allow reports policy index 0");
    allowResult.RequireAuthorized();
    Check(true, "RequireAuthorized passes on clean allow");

    // 23. Deny without a match is a result, never an exception.
    // (The token carries read+write, so "delete" genuinely matches nothing.)
    BiscuitAuthorizationResult denyResult = BiscuitAuthorizer
        .For(token)
        .AddFact("""operation("delete")""")
        .AddPolicy("""allow if right("workspace.main", "delete");""")
        .Authorize();
    Check(denyResult.Decision == BiscuitDecision.Deny && !denyResult.IsAuthorized, "non-matching request is denied");
    Check(denyResult.Errors.Any(e => e.Code == "no_matching_policy"), "deny names no_matching_policy");
    bool threw = false;
    try
    {
        denyResult.RequireAuthorized();
    }
    catch (BiscuitAuthorizationException ex)
    {
        threw = ex.Result is not null && ex.Result.Decision == BiscuitDecision.Deny;
    }

    Check(threw, "RequireAuthorized throws carrying the denial");

    // 24. Allow matched but attenuation check failed: deny with failed checks.
    BiscuitAuthorizationResult narrowed = BiscuitAuthorizer
        .For(child)
        .AddFact("""operation("write")""")
        .AddPolicy("""allow if right("workspace.main", "read");""")
        .Authorize();
    Check(narrowed.Decision == BiscuitDecision.Deny, "attenuated-away request is denied");
    Check(narrowed.AllowPolicyIndex == 0, "matched allow policy is reported");
    IReadOnlyList<BiscuitAuthorizationError> failed =
        narrowed.Errors.Where(e => e.Code == "failed_check").ToList();
    Check(failed.Count > 0, "failed checks are reported");
    Check(failed.All(e => e.BlockId.HasValue && e.Rule is not null), "failed checks carry block and rule");
    Check(failed.Any(e => e.Rule!.Contains("operation", StringComparison.Ordinal)), "failed rule text is shown");

    // 25. Explicit deny first: first-match-wins in policy order.
    BiscuitAuthorizationResult denied = BiscuitAuthorizer
        .For(token)
        .AddFact("""operation("read")""")
        .AddFact("""banned("workspace.main")""")
        .AddPolicy("""deny if banned("workspace.main");""")
        .AddPolicy("""allow if right("workspace.main", "read");""")
        .Authorize();
    Check(denied.Decision == BiscuitDecision.Deny, "explicit deny denies");
    Check(denied.DenyPolicyIndex == 0, "deny reports policy index 0");
    Check(denied.Errors.Any(e => e.Code == "deny_policy_matched"), "deny names the matched policy");

    // 26. Malformed Datalog never becomes a decision.
    Check(Throws<BiscuitDatalogException>(() => BiscuitAuthorizer.For(token).AddPolicy("allow if").Authorize()), "malformed policy is a Datalog error");
    Check(Throws<BiscuitDatalogException>(() => BiscuitAuthorizer.For(token).AddFact("fact(").AddPolicy("""allow if right("workspace.main", "read");""").Authorize()), "malformed fact is a Datalog error");
    Check(Throws<ArgumentException>(() => BiscuitAuthorizer.For(token).AddPolicy("  ")), "empty policy fails fast");

    // 27. Repeated authorization is deterministic.
    BiscuitAuthorizer reusable = BiscuitAuthorizer
        .For(token)
        .AddFact("""operation("read")""")
        .AddPolicy("""allow if right("workspace.main", "read");""");
    BiscuitAuthorizationResult first = reusable.Authorize();
    BiscuitAuthorizationResult second = reusable.Authorize();
    Check(first.Decision == second.Decision, "repeated decisions agree");
    Check(first.Errors.Count == second.Errors.Count, "repeated errors agree");
    Check(first.AllowPolicyIndex == second.AllowPolicyIndex, "repeated indices agree");

    // 28. P-256 authorizes; concurrency holds.
    BiscuitAuthorizationResult p256Allow = BiscuitAuthorizer
        .For(p256Token)
        .AddFact("""operation("read")""")
        .AddPolicy("""allow if right("a", "read");""")
        .Authorize();
    Check(p256Allow.IsAuthorized, "P-256 request is authorized");
    int authErrors = 0;
    Parallel.For(0, 32, _ =>
    {
        try
        {
            var r = BiscuitAuthorizer
                .For(token)
                .AddFact("""operation("read")""")
                .AddPolicy("""allow if right("workspace.main", "read");""")
                .Authorize();
            if (!r.IsAuthorized)
            {
                Interlocked.Increment(ref authErrors);
            }
        }
        catch
        {
            Interlocked.Increment(ref authErrors);
        }
    });
    Check(authErrors == 0, "concurrent authorization");

    // 29. Committed interop fixtures: direct-Rust-generated, always present.
    string repoRoot = FindRepoRoot();
    Check(repoRoot != "", "repository root located");
    string genesisPath = repoRoot == "" ? "" : Path.Combine(repoRoot, "fixtures", "compat", "genesis.json");
    Check(genesisPath != "" && File.Exists(genesisPath), "committed genesis fixture present");
    if (genesisPath != "" && File.Exists(genesisPath))
    {
        using JsonDocument genesisDoc = JsonDocument.Parse(File.ReadAllBytes(genesisPath));
        JsonElement fx = genesisDoc.RootElement;
        var fxRoot = new BiscuitPublicKey(
            Convert.FromBase64String(FxString(fx, "public_b64")),
            FxString(fx, "algorithm") == "ed25519" ? BiscuitKeyAlgorithm.Ed25519 : BiscuitKeyAlgorithm.P256);
        BiscuitToken fxToken = BiscuitToken.Parse(Convert.FromBase64String(FxString(fx, "token_b64")), fxRoot);
        Check(fxToken.Inspect().BlockSources[0].Contains("workspace.main", StringComparison.Ordinal), "fixture token verifies with expected content");
        BiscuitAuthorizationResult fxAuth = BiscuitAuthorizer
            .For(fxToken)
            .AddFact("""operation("read")""")
            .AddPolicy("""allow if right("workspace.main", "read");""")
            .Authorize();
        Check(fxAuth.IsAuthorized, "fixture token authorizes");
        using BiscuitPrivateKey fxKey = BiscuitPrivateKey.Import(Convert.FromBase64String(FxString(fx, "private_der_b64")));
        Check(fxKey.PublicKey.Encoded.SequenceEqual(fxRoot.Encoded), "fixture private import reproduces the root");
    }

    // 30. Generated exchange (requires Rust fixtures from compat_gen). Skipped in
    // consume mode so the prior run's fixtures survive for section 31.
    bool compatConsumeOnly = Environment.GetEnvironmentVariable("BISCUITSHARP_COMPAT_CONSUME") == "1";
    string compatDir = repoRoot == "" ? "" : Path.Combine(repoRoot, "artifacts", "compat");
    string rustFxDir = compatDir == "" ? "" : Path.Combine(compatDir, "rust");
    string managedFxDir = compatDir == "" ? "" : Path.Combine(compatDir, "managed");
    string rustRootPath = rustFxDir == "" ? "" : Path.Combine(rustFxDir, "root.json");
    string rustParentPath = rustFxDir == "" ? "" : Path.Combine(rustFxDir, "parent.json");
    if (!compatConsumeOnly)
    {
    if (rustRootPath != "" && File.Exists(rustRootPath) && File.Exists(rustParentPath))
    {
        Directory.CreateDirectory(managedFxDir);
        using JsonDocument rustRootDoc = JsonDocument.Parse(File.ReadAllBytes(rustRootPath));
        using JsonDocument rustParentDoc = JsonDocument.Parse(File.ReadAllBytes(rustParentPath));
        using BiscuitPrivateKey compatRoot = BiscuitPrivateKey.Import(Convert.FromBase64String(FxString(rustRootDoc.RootElement, "private_der_b64")));
        BiscuitPublicKey compatPub = compatRoot.PublicKey;
        // Direction 1: the Rust-issued parent verifies and authorizes through the bridge.
        BiscuitToken compatParent = BiscuitToken.Parse(Convert.FromBase64String(FxString(rustParentDoc.RootElement, "token_b64")), compatPub);
        Check(compatParent.Inspect().BlockCount == 1, "Rust parent verifies through the bridge");
        BiscuitAuthorizationResult compatParentAuth = BiscuitAuthorizer
            .For(compatParent)
            .AddFact("""operation("read")""")
            .AddPolicy("""allow if right("workspace.main", "read");""")
            .Authorize();
        Check(compatParentAuth.IsAuthorized, "Rust parent authorizes through the bridge");
        // Direction 3 setup: the bridge attenuates the Rust parent for direct-Rust verification.
        BiscuitToken compatChild = compatParent.Attenuate(BiscuitBlock.Create("""check if operation("read");"""));
        Check(BiscuitToken.Parse(compatChild.ToBytes(), compatPub) == compatChild, "attenuated Rust parent re-verifies");
        File.WriteAllText(
            Path.Combine(managedFxDir, "child.json"),
            $"{{\"token_b64\":\"{Convert.ToBase64String(compatChild.ToBytes())}\"}}");
        // Direction 2 setup: a bridge-issued token for direct-Rust verification.
        using BiscuitPrivateKey managedRootKey = BiscuitPrivateKey.Generate();
        BiscuitToken managedIssued = BiscuitTokenBuilder
            .Create()
            .AddFact("""right("workspace.main", "read")""")
            .AddFact("""perms("repo", {1, 2})""")
            .Build(managedRootKey);
        BiscuitAuthorizationResult managedSanity = BiscuitAuthorizer
            .For(managedIssued)
            .AddFact("""operation("read")""")
            .AddPolicy("""allow if right("workspace.main", "read");""")
            .Authorize();
        Check(managedSanity.IsAuthorized, "bridge-issued token authorizes locally");
        File.WriteAllText(
            Path.Combine(managedFxDir, "token.json"),
            $"{{\"algorithm\":\"{(managedRootKey.Algorithm == BiscuitKeyAlgorithm.Ed25519 ? "ed25519" : "secp256r1")}\",\"public_b64\":\"{Convert.ToBase64String(managedRootKey.PublicKey.Encoded)}\",\"token_b64\":\"{Convert.ToBase64String(managedIssued.ToBytes())}\"}}");
    }
    else
    {
        Console.WriteLine("SKIP: Rust compat fixtures absent (run eng/Test-Compat.ps1); skipping generated exchange.");
    }
    }
    else
    {
        Console.WriteLine("SKIP: generated exchange (consume mode; using the prior run's fixtures).");
    }

    // 31. Consume phase: the direct-Rust-attenuated child of the managed token.
    if (compatConsumeOnly)
    {
        string child2Path = rustFxDir == "" ? "" : Path.Combine(rustFxDir, "child2.json");
        string managedTokenPath = managedFxDir == "" ? "" : Path.Combine(managedFxDir, "token.json");
        Check(child2Path != "" && File.Exists(child2Path) && File.Exists(managedTokenPath), "compat consume fixtures present");
        if (child2Path != "" && File.Exists(child2Path) && File.Exists(managedTokenPath))
        {
            using JsonDocument managedTokenDoc = JsonDocument.Parse(File.ReadAllBytes(managedTokenPath));
            var consumeRoot = new BiscuitPublicKey(
                Convert.FromBase64String(FxString(managedTokenDoc.RootElement, "public_b64")),
                FxString(managedTokenDoc.RootElement, "algorithm") == "ed25519" ? BiscuitKeyAlgorithm.Ed25519 : BiscuitKeyAlgorithm.P256);
            using JsonDocument child2Doc = JsonDocument.Parse(File.ReadAllBytes(child2Path));
            BiscuitToken consumedChild = BiscuitToken.Parse(Convert.FromBase64String(FxString(child2Doc.RootElement, "token_b64")), consumeRoot);
            Check(consumedChild.Inspect().BlockCount == 2, "Rust-attenuated child verifies through the bridge");
            BiscuitAuthorizationResult consumedAuth = BiscuitAuthorizer
                .For(consumedChild)
                .AddFact("""operation("read")""")
                .AddPolicy("""allow if right("workspace.main", "read");""")
                .Authorize();
            Check(consumedAuth.IsAuthorized, "Rust-attenuated child authorizes through the bridge");
        }
    }
    else
    {
        Console.WriteLine("SKIP: compat consume phase (set BISCUITSHARP_COMPAT_CONSUME=1 after the Rust consume step).");
    }

    // 32. Adversarial: systematically mutated tokens never verify; invalid
    // policies either fail fast as Datalog errors or deny — never authorize.
    byte[] canonical = token.ToBytes();
    int mutationSurvivors = 0;
    var positions = Enumerable
        .Range(0, Math.Min(64, canonical.Length))
        .Concat(Enumerable.Range(Math.Max(0, canonical.Length - 32), Math.Min(32, canonical.Length)))
        .Distinct()
        .ToList();
    foreach (int pos in positions)
    {
        byte[] mutant = (byte[])canonical.Clone();
        mutant[pos] ^= 0xFF;
        try
        {
            BiscuitToken.Parse(mutant, rootKey.PublicKey);
            mutationSurvivors++;
        }
        catch (BiscuitTokenException)
        {
        }
    }

    Check(mutationSurvivors == 0, "mutated tokens never verify");
    string[] invalidPolicies = new[]
    {
        "allow if",
        "deny if",
        "allow if right(",
        "allow if right(\"a\")",
        "permit(principal, action, resource);",
        "allow if 1 == 2;",
        "allow if operation(",
    };
    int policyFailures = 0;
    foreach (string badPolicy in invalidPolicies)
    {
        bool closed = false;
        try
        {
            BiscuitAuthorizationResult r = BiscuitAuthorizer
                .For(token)
                .AddFact("""operation("read")""")
                .AddPolicy(badPolicy)
                .Authorize();
            closed = !r.IsAuthorized;
        }
        catch (BiscuitDatalogException)
        {
            closed = true;
        }

        if (!closed)
        {
            policyFailures++;
        }
    }

    Check(policyFailures == 0, "invalid policies never authorize");

    // 33. Value equality over key/revocation material (array bytes, not identity).
    var replicatedKey = new BiscuitPublicKey((byte[])rootKey.PublicKey.Encoded.Clone(), rootKey.Algorithm);
    Check(replicatedKey == rootKey.PublicKey, "public keys compare by value");
    Check(replicatedKey.GetHashCode() == rootKey.PublicKey.GetHashCode(), "equal public keys hash equally");
    Check(replicatedKey != p256Root.PublicKey, "different public keys are unequal");
    BiscuitRevocationId revIdA = token.GetRevocationIds()[0];
    BiscuitRevocationId revIdB = BiscuitToken.Parse(token.ToBytes(), rootKey.PublicKey).GetRevocationIds()[0];
    Check(revIdA == revIdB, "revocation ids from separate parses compare equal");
    Check(new HashSet<BiscuitRevocationId> { revIdA }.Contains(revIdB), "revocation ids work as dictionary keys");

    // 34. PEM export round-trips through Import (asymmetric with DER Export()).
    string pemExport = edKey.ExportPem();
    Check(pemExport.StartsWith("-----BEGIN PRIVATE KEY-----", StringComparison.Ordinal), "PEM export is armored");
    using BiscuitPrivateKey fromExportPem = BiscuitPrivateKey.Import(Encoding.ASCII.GetBytes(pemExport));
    Check(fromExportPem.PublicKey == edKey.PublicKey, "PEM export round-trips the public half");
    Check(!edKey.ToString().Contains(pemExport, StringComparison.Ordinal), "ToString never exposes exported PEM");

    // 35. Explicit ambient time fact drives expiration checks deterministically.
    BiscuitToken expiring = BiscuitTokenBuilder
        .Create()
        .AddFact("""right("a", "read")""")
        .AddCheck("""check if time($t), $t < 2030-01-01T00:00:00Z;""")
        .Build(rootKey);
    BiscuitAuthorizationResult beforeExpiry = BiscuitAuthorizer
        .For(expiring)
        .AddTimeFact(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero))
        .AddPolicy("""allow if right("a", "read");""")
        .Authorize();
    Check(beforeExpiry.IsAuthorized, "time fact before expiry allows");
    BiscuitAuthorizationResult afterExpiry = BiscuitAuthorizer
        .For(expiring)
        .AddTimeFact(new DateTimeOffset(2031, 1, 1, 0, 0, 0, TimeSpan.Zero))
        .AddPolicy("""allow if right("a", "read");""")
        .Authorize();
    Check(afterExpiry.Decision == BiscuitDecision.Deny, "time fact after expiry denies");
    Check(
        afterExpiry.Errors.Any(e => e.Code == "failed_check"),
        "expired check is reported as a failed check");

    // 36. Public-key Parse validates without the private half.
    BiscuitPublicKey parsedEd = BiscuitPublicKey.Parse(edKey.PublicKey.Encoded, BiscuitKeyAlgorithm.Ed25519);
    Check(parsedEd == edKey.PublicKey, "Ed25519 public import round-trips");
    BiscuitPublicKey parsedP256 = BiscuitPublicKey.Parse(p256Key.PublicKey.Encoded, BiscuitKeyAlgorithm.P256);
    Check(parsedP256 == p256Key.PublicKey, "P-256 public import round-trips");
    Check(Throws<BiscuitKeyException>(() => BiscuitPublicKey.Parse(Array.Empty<byte>(), BiscuitKeyAlgorithm.Ed25519)), "empty public import fails");
    Check(Throws<BiscuitKeyException>(() => BiscuitPublicKey.Parse(new byte[] { 1, 2, 3 }, BiscuitKeyAlgorithm.Ed25519)), "short public import fails");
    Check(Throws<ArgumentOutOfRangeException>(() => BiscuitPublicKey.Parse(edKey.PublicKey.Encoded, (BiscuitKeyAlgorithm)42)), "unknown public algorithm fails fast");
    Check(
        Throws<BiscuitKeyException>(() => BiscuitPublicKey.Parse(edKey.PublicKey.Encoded, BiscuitKeyAlgorithm.P256)),
        "cross-algorithm public import fails");

    // 37. Rules derive facts in the authority block and the authorizer scope.
    BiscuitToken ruled = BiscuitTokenBuilder
        .Create()
        .AddFact("""role("admin")""")
        .AddRule("""right("a", "read") <- role("admin");""")
        .Build(rootKey);
    BiscuitAuthorizationResult ruledAuth = BiscuitAuthorizer
        .For(ruled)
        .AddPolicy("""allow if right("a", "read");""")
        .Authorize();
    Check(ruledAuth.IsAuthorized, "token rule derives the allowed fact");
    BiscuitAuthorizationResult scopeRuled = BiscuitAuthorizer
        .For(token)
        .AddFact("""role("admin")""")
        .AddRule("""right("a", "read") <- role("admin");""")
        .AddPolicy("""allow if right("a", "read");""")
        .Authorize();
    Check(scopeRuled.IsAuthorized, "authorizer rule derives the allowed fact");
    Check(Throws<BiscuitDatalogException>(() => BiscuitTokenBuilder.Create().AddRule("right(").Build(rootKey)), "malformed builder rule fails");
    Check(Throws<BiscuitDatalogException>(() => BiscuitAuthorizer.For(token).AddRule("right(").AddPolicy("""allow if right("a", "read");""").Authorize()), "malformed authorizer rule fails");
    Check(Throws<ArgumentException>(() => BiscuitTokenBuilder.Create().AddRule("  ")), "empty rule fails fast");

    // 38. Load-time manifest verification, via isolated child probes (the
    // loader caches per process, so each scenario gets a fresh process).
    string probeDir = Path.Combine(Path.GetTempPath(), "biscuit-sharp-manifest-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(probeDir);
    try
    {
        string probeDll = Path.Combine(probeDir, Path.GetFileName(asset));
        File.Copy(asset, probeDll);
        BiscuitSharpVersionInfo live = BiscuitEngine.GetVersion();
        string probeManifest = Path.Combine(probeDir, "biscuitsharp-native.json");
        File.WriteAllText(probeManifest, BuildProbeManifest(live, live.NativeSha256));
        Check(RunProbe(probeDll) == 0, "manifest-verified load succeeds");
        File.WriteAllText(probeManifest, BuildProbeManifest(live, new string('0', 64)));
        Check(RunProbe(probeDll) != 0, "tampered manifest refuses load");
    }
    finally
    {
        try { Directory.Delete(probeDir, true); } catch { }
    }
}
finally
{
    Environment.SetEnvironmentVariable("BISCUITSHARP_NATIVE_PATH", previousOverride);
}

static string FindRepoRoot()
{
    string? dir = AppContext.BaseDirectory;
    for (int i = 0; i < 10 && dir != null; i++)
    {
        if (File.Exists(Path.Combine(dir, "native", "Cargo.toml")))
        {
            return dir;
        }

        dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    return "";
}

static string FxString(JsonElement root, string field) =>
    root.TryGetProperty(field, out JsonElement value) && value.ValueKind == JsonValueKind.String
        ? value.GetString() ?? throw new InvalidOperationException($"Fixture field '{field}' is null.")
        : throw new InvalidOperationException($"Fixture is missing string field '{field}'.");

/// Child-probe entry: loads exactly one native asset (via override) and
/// reports whether manifest verification accepted it. Runs in a fresh process
/// because the loader caches per process.
static int ManifestProbe(string dllPath)
{
    Environment.SetEnvironmentVariable("BISCUITSHARP_NATIVE_PATH", dllPath);
    try
    {
        BiscuitSharpVersionInfo version = BiscuitEngine.GetVersion();
        Console.WriteLine($"MANIFEST-OK abi={version.AbiVersion} biscuit-auth={version.BiscuitAuthVersion}");
        return 0;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"MANIFEST-FAIL {ex.GetType().Name}: {ex.Message.Split('\n')[0]}");
        return 1;
    }
}

static string BuildProbeManifest(BiscuitSharpVersionInfo live, string binarySha256)
{
    string features = string.Join(",", live.EnabledFeatures.Split(',').Select(f => $"\"{f}\""));
    return "{\"binary_sha256\":\"" + binarySha256
        + "\",\"abi\":" + live.AbiVersion
        + ",\"biscuit_auth\":\"" + live.BiscuitAuthVersion
        + "\",\"bridge\":\"" + live.BridgeVersion
        + "\",\"upstream_commit\":\"" + live.UpstreamCommit
        + "\",\"cargo_lock_sha256\":\"" + live.CargoLockHash
        + "\",\"target\":\"" + live.TargetTriple
        + "\",\"rid\":\"" + live.RuntimeIdentifier
        + "\",\"rust\":\"" + live.RustVersion
        + "\",\"enabled_features\":[" + features + "]}";
}

static int RunProbe(string dllPath)
{
    string repoRoot = FindRepoRoot();
    string project = Path.Combine(repoRoot, "tests", "BiscuitSharp.Tests", "BiscuitSharp.Tests.csproj");
    // Prefer the current muxer; fall back to PATH lookup.
    string? muxer = Environment.ProcessPath;
    string fileName;
    string arguments;
    if (muxer is not null && Path.GetFileName(muxer).StartsWith("dotnet", StringComparison.OrdinalIgnoreCase))
    {
        fileName = muxer;
        arguments = $"run --project \"{project}\" --framework net8.0 -- --manifest-probe \"{dllPath}\"";
    }
    else
    {
        fileName = "dotnet";
        arguments = $"run --project \"{project}\" --framework net8.0 -- --manifest-probe \"{dllPath}\"";
    }

    using var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        },
    };
    var output = new StringBuilder();
    process.OutputDataReceived += (_, e) => { if (e.Data is not null) { output.AppendLine(e.Data); } };
    process.Start();
    process.BeginOutputReadLine();
    if (!process.WaitForExit(300000))
    {
        try { process.Kill(); } catch { }
        Console.WriteLine("Probe timed out.");
        return 2;
    }

    Console.WriteLine(output.ToString().Trim());
    return process.ExitCode;
}

static string FindNativeAsset(string fileName)
{
    string? overridePath = Environment.GetEnvironmentVariable("BISCUITSHARP_NATIVE_PATH");
    if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
    {
        return overridePath;
    }

    string? dir = AppContext.BaseDirectory;
    for (int i = 0; i < 10 && dir != null; i++)
    {
        foreach (string config in new[] { "debug", "release" })
        {
            string candidate = Path.Combine(dir, "native", "target", config, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    return "";
}

static string? FindCargoLock(string assetPath)
{
    // asset is <root>/native/target/<config>/<file>; lock is <root>/native/Cargo.lock.
    string? dir = Path.GetDirectoryName(assetPath);
    for (int i = 0; i < 4 && dir != null; i++)
    {
        string candidate = Path.Combine(dir, "Cargo.lock");
        if (File.Exists(candidate))
        {
            return candidate;
        }

        dir = Path.GetDirectoryName(dir);
    }

    return null;
}

static string ChunkBase64(byte[] bytes)
{
    // PEM-style 64-column wrapping with LF endings.
    string raw = Convert.ToBase64String(bytes);
    var sb = new StringBuilder((raw.Length / 64 + 2) * 65);
    for (int i = 0; i < raw.Length; i += 64)
    {
        sb.Append(raw, i, Math.Min(64, raw.Length - i));
        sb.Append('\n');
    }

    return sb.ToString();
}

Console.WriteLine(failures == 0 ? "M1 checks passed." : $"{failures} check(s) failed.");
return failures;
