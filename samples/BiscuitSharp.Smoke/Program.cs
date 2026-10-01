// Framework-dependent smoke: reports the loaded bridge identity. Requires a
// built native asset (`cargo build --locked` in native/) or BISCUITSHARP_NATIVE_PATH.
using BiscuitSharp;

try
{
    var version = BiscuitEngine.GetVersion();
    Console.WriteLine($"bridge={version.BridgeVersion} abi={version.AbiVersion} biscuit-auth={version.BiscuitAuthVersion}");
}
catch (BiscuitBridgeException ex)
{
    Console.WriteLine($"Native asset unavailable (build it first): {ex.Message}");
}
