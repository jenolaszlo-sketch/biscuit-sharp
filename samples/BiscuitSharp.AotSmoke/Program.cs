// NativeAOT smoke: publish + execute per qualified RID in CI (M2).
using BiscuitSharp;

try
{
    var version = BiscuitEngine.GetVersion();
    Console.WriteLine($"AOT smoke: bridge={version.BridgeVersion} abi={version.AbiVersion}");
}
catch (BiscuitBridgeException ex)
{
    Console.WriteLine($"Scaffolding mode (expected until M1): {ex.Message}");
}
