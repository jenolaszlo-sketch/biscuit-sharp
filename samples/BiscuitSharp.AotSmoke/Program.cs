// NativeAOT smoke: published and executed per qualified RID in CI (M2).
using BiscuitSharp;

try
{
    var version = BiscuitEngine.GetVersion();
    Console.WriteLine($"AOT smoke: bridge={version.BridgeVersion} abi={version.AbiVersion}");
}
catch (BiscuitBridgeException ex)
{
    Console.WriteLine($"Native asset unavailable (build it first): {ex.Message}");
}
