using System;

namespace LibHac.FsSrv
{
    public delegate Result RandomDataGenerator(Span<byte> buffer);

    public delegate Result SaveTransferAesKeyGenerator(Span<byte> key,
        SaveDataTransferCryptoConfiguration.KeyIndex index, ReadOnlySpan<byte> keySource, int keyGeneration);

    public delegate Result SaveTransferCmacGenerator(Span<byte> mac, ReadOnlySpan<byte> data,
        SaveDataTransferCryptoConfiguration.KeyIndex index, int keyGeneration);

    public delegate Result PatrolAllocateCountGetter(out long successCount, out long failureCount);
}
