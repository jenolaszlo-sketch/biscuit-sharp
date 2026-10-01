// Scaffolding test runner (no external test framework yet).
// M1 replaces these placeholder checks with the native/managed matrices in docs/implementation-plan.md.
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

// Scaffolding: every operation must fail closed with BiscuitBridgeException until the native bridge lands.
Check(Throws<BiscuitBridgeException>(() => BiscuitPrivateKey.Generate()), "key generate fails closed without native");
Check(Throws<BiscuitBridgeException>(() => BiscuitPrivateKey.Import(new byte[] { 1, 2, 3 })), "key import fails closed without native");
Check(Throws<BiscuitBridgeException>(() => BiscuitEngine.GetVersion()), "version discovery fails closed without native");
Check(NativeBridge.GetNativeFileName("win-x64") == "biscuitsharp_native.dll", "win-x64 native filename");
Check(NativeBridge.GetNativeFileName("linux-x64") == "libbiscuitsharp_native.so", "linux-x64 native filename");
Check(NativeBridge.GetNativeFileName("osx-arm64") == "libbiscuitsharp_native.dylib", "osx-arm64 native filename");

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

Console.WriteLine(failures == 0 ? "Scaffolding checks passed." : $"{failures} scaffolding check(s) failed.");
return failures;
