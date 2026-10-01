// Framework-dependent smoke: exercises the M1 flow once the native bridge lands.
// Until then it reports the expected scaffolding failure and exits 0 (scaffolding mode).
using BiscuitSharp;

try
{
    var version = BiscuitEngine.GetVersion();
    Console.WriteLine($"bridge={version.BridgeVersion} abi={version.AbiVersion} biscuit-auth={version.BiscuitAuthVersion}");
}
catch (BiscuitBridgeException ex)
{
    Console.WriteLine($"Scaffolding mode (expected until M1): {ex.Message}");
}
