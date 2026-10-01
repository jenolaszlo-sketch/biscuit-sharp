using System.Reflection;
using System.Runtime.InteropServices;
using BiscuitSharp;

Type loader = typeof(BiscuitEngine).Assembly.GetType("BiscuitSharp.NativeLoader", throwOnError: true)!;
MethodInfo selector = loader.GetMethod(
    "SelectRuntimeIdentifier",
    BindingFlags.Static | BindingFlags.NonPublic) ?? throw new InvalidOperationException("RID selector seam is missing.");

var cases = new (bool Windows, bool Linux, bool OSX, Architecture ProcessArchitecture, string? Expected)[]
{
    (true, false, false, Architecture.X64, "win-x64"),
    (false, true, false, Architecture.X64, "linux-x64"),
    (false, false, true, Architecture.Arm64, "osx-arm64"),
    (true, false, false, Architecture.X86, null),
    (true, false, false, Architecture.Arm64, null),
    (false, true, false, Architecture.Arm64, null),
    (false, false, true, Architecture.X64, null),
    (false, false, false, Architecture.X64, null),
};

foreach (var testCase in cases)
{
    object? actual = selector.Invoke(null,
    [
        testCase.Windows,
        testCase.Linux,
        testCase.OSX,
        testCase.ProcessArchitecture,
    ]);
    if (!Equals(actual, testCase.Expected))
    {
        throw new InvalidOperationException(
            $"RID selection for {testCase} returned '{actual ?? "null"}', expected '{testCase.Expected ?? "null"}'.");
    }
}

string runtimeRid = (string)(loader.GetMethod(
    "GetRuntimeIdentifier",
    BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, null)
    ?? throw new InvalidOperationException("Host runtime RID was null."));

string? expectedRuntimeRid = (string?)selector.Invoke(null,
[
    RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
    RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
    RuntimeInformation.IsOSPlatform(OSPlatform.OSX),
    RuntimeInformation.ProcessArchitecture,
]);
if (!string.Equals(runtimeRid, expectedRuntimeRid, StringComparison.Ordinal))
{
    throw new InvalidOperationException(
        $"Host RID '{runtimeRid}' did not match the ProcessArchitecture selection '{expectedRuntimeRid}'.");
}

Console.WriteLine($"Runtime identifier matrix passed ({cases.Length} mappings; host {runtimeRid}).");
