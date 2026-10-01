namespace BiscuitSharp;

/// <summary>Safe token inspection without authorization. Never exposes private signing material.</summary>
public sealed record BiscuitInspection(
    int BlockCount,
    bool IsSealed,
    BiscuitKeyAlgorithm SignatureAlgorithm,
    BiscuitKeyAlgorithm RootKeyAlgorithm,
    IReadOnlyList<BiscuitRevocationId> RevocationIds,
    IReadOnlyList<string> BlockSources,
    long TokenSizeBytes,
    string? FormatVersion);
