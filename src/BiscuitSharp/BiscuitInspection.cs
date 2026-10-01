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
    string? FormatVersion,
    uint? RootKeyId)
{
    private IReadOnlyList<BiscuitRevocationId> _revocationIds = ImmutableSnapshot.Copy(RevocationIds, nameof(RevocationIds));
    private IReadOnlyList<string> _blockSources = ImmutableSnapshot.Copy(BlockSources, nameof(BlockSources));

    public IReadOnlyList<BiscuitRevocationId> RevocationIds
    {
        get => _revocationIds;
        init => _revocationIds = ImmutableSnapshot.Copy(value, nameof(RevocationIds));
    }

    public IReadOnlyList<string> BlockSources
    {
        get => _blockSources;
        init => _blockSources = ImmutableSnapshot.Copy(value, nameof(BlockSources));
    }
}
