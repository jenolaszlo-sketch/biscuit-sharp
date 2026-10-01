// M1 test runner (no external test framework yet): version/loader + key slices
// against the real native asset. Requires a built bridge:
// `cargo build --locked` in native/.
// Full native/managed matrices live in docs/implementation-plan.md.
using System.Security.Cryptography;
using System.Text;
using BiscuitSharp;

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
}
finally
{
    Environment.SetEnvironmentVariable("BISCUITSHARP_NATIVE_PATH", previousOverride);
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

Console.WriteLine(failures == 0 ? "M1 version+key+token checks passed." : $"{failures} check(s) failed.");
return failures;
