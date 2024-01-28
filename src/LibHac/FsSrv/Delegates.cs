using System;
using LibHac.Common;

namespace LibHac.FsSrv;

public delegate Result SaveTransferAesKeyGenerator(Span<byte> outKeyBuffer,
    SaveDataTransferCryptoConfiguration.KeyIndex index, ReadOnlySpan<byte> keySource, int keyGeneration);

public delegate Result SaveTransferCmacGenerator(Span<byte> outMacBuffer, ReadOnlySpan<byte> data,
    SaveDataTransferCryptoConfiguration.Attributes attribute, SaveDataTransferCryptoConfiguration.KeyIndex index,
    int keyGeneration);

public delegate Result SaveTransferOpenDecryptor(
    ref SharedRef<SaveDataTransferCryptoConfiguration.IDecryptor> outDecryptor,
    SaveDataTransferCryptoConfiguration.Attributes attribute, SaveDataTransferCryptoConfiguration.KeyIndex keyIndex,
    ReadOnlySpan<byte> keySource, int keyGeneration, ReadOnlySpan<byte> iv, ReadOnlySpan<byte> mac);

public delegate Result SaveTransferOpenEncryptor(
    ref SharedRef<SaveDataTransferCryptoConfiguration.IEncryptor> outEncryptor,
    SaveDataTransferCryptoConfiguration.Attributes attribute, SaveDataTransferCryptoConfiguration.KeyIndex keyIndex,
    ReadOnlySpan<byte> keySource, int keyGeneration, ReadOnlySpan<byte> iv);

public delegate bool VerifyRsaSignature(ReadOnlySpan<byte> signature, ReadOnlySpan<byte> modulus,
    ReadOnlySpan<byte> exponent, ReadOnlySpan<byte> data);

public delegate Result PatrolAllocateCountGetter(out long successCount, out long failureCount);