using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Crypto;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Kernel;

namespace LibHac.Boot;

/// <summary>
/// Parses a package2 file and opens the payloads within.
/// </summary>
public class Package2StorageReader : IDisposable
{
    private const int KernelPayloadIndex = 0;
    private const int IniPayloadIndex = 1;

    private SharedRef<IStorage> _storage;
    private Package2Header _header;
    private KeySet _keySet;
    private Crypto.AesKey _key;

    public ref readonly Package2Header Header => ref _header;

    public void Dispose()
    {
        _storage.Destroy();
    }

    /// <summary>
    /// Initializes the <see cref="Package2StorageReader"/>.
    /// </summary>
    /// <param name="keySet">The keyset to use for decrypting the package.</param>
    /// <param name="storage">An <see cref="IStorage"/> of the encrypted package2.</param>
    /// <returns>The <see cref="Result"/> of the operation.</returns>
    public Result Initialize(KeySet keySet, in SharedRef<IStorage> storage)
    {
        Result rc = storage.Get.Read(0, SpanHelpers.AsByteSpan(ref _header));
        if (rc.IsFailure()) return rc;

        _key = keySet.Package2Keys[_header.Meta.KeyGeneration];
        DecryptHeader(_key, ref _header.Meta, ref _header.Meta);

        _storage.SetByCopy(in storage);
        _keySet = keySet;
        return Result.Success;
    }

    /// <summary>
    /// Opens a decrypted <see cref="IStorage"/> of one of the payloads in the package.
    /// </summary>
    /// <param name="outPayloadStorage">If the method returns successfully, contains an <see cref="IStorage"/>
    /// of the specified payload.</param>
    /// <param name="index">The index of the payload to get. Must me less than <see cref="Package2Header.PayloadCount"/></param>
    /// <returns>The <see cref="Result"/> of the operation.</returns>
    public Result OpenPayload(ref UniqueRef<IStorage> outPayloadStorage, int index)
    {
        if ((uint)index >= Package2Header.PayloadCount)
            return ResultLibHac.ArgumentOutOfRange.Log();

        int offset = _header.Meta.GetPayloadFileOffset(index);
        int size = (int)_header.Meta.PayloadSizes[index];

        var payloadSubStorage = new SubStorage(_storage, offset, size);

        if (size == 0)
        {
            outPayloadStorage.Reset(payloadSubStorage);
            return Result.Success;
        }

        byte[] iv = _header.Meta.PayloadIvs[index].Bytes.ToArray();
        outPayloadStorage.Reset(new CachedStorage(new Aes128CtrStorage(payloadSubStorage, _key.DataRo.ToArray(), iv, true), 0x4000, 1, true));
        return Result.Success;
    }

    /// <summary>
    /// Opens an <see cref="IStorage"/> of the kernel payload.
    /// </summary>
    /// <param name="outKernelStorage">If the method returns successfully, contains an <see cref="IStorage"/>
    /// of the kernel payload.</param>
    /// <returns>The <see cref="Result"/> of the operation.</returns>
    public Result OpenKernel(ref UniqueRef<IStorage> outKernelStorage)
    {
        return OpenPayload(ref outKernelStorage, KernelPayloadIndex);
    }

    /// <summary>
    /// Opens an <see cref="IStorage"/> of the initial process binary. If the binary is embedded in
    /// the kernel, this method will attempt to locate and return the embedded binary.
    /// </summary>
    /// <param name="outIniStorage">If the method returns successfully, contains an <see cref="IStorage"/>
    /// of the initial process binary.</param>
    /// <returns>The <see cref="Result"/> of the operation.</returns>
    public Result OpenIni(ref UniqueRef<IStorage> outIniStorage)
    {
        if (HasIniPayload())
        {
            return OpenPayload(ref outIniStorage, IniPayloadIndex);
        }

        // Ini is embedded in the kernel
        using var kernelStorage = new UniqueRef<IStorage>();
        Result rc = OpenKernel(ref kernelStorage.Ref());
        if (rc.IsFailure()) return rc;

        if (!IniExtract.TryGetIni1Offset(out int offset, out int size, kernelStorage.Get))
        {
            // Unable to find the ini. Could be a new, unsupported layout.
            return ResultLibHac.NotImplemented.Log();
        }

        outIniStorage.Reset(new SubStorage(kernelStorage.Release(), offset, size));
        return Result.Success;
    }

    /// <summary>
    /// Verifies the signature, metadata and payloads in the package.
    /// </summary>
    /// <returns>The <see cref="Result"/> of the operation.</returns>
    public Result Verify()
    {
        Result rc = VerifySignature();
        if (rc.IsFailure()) return rc;

        rc = VerifyMeta();
        if (rc.IsFailure()) return rc;

        return VerifyPayloads();
    }

    /// <summary>
    /// Verifies the signature of the package.
    /// </summary>
    /// <returns>The <see cref="Result"/> of the operation.
    /// <see cref="Result.Success"/> if the signature is valid.</returns>
    public Result VerifySignature()
    {
        Unsafe.SkipInit(out Package2Meta meta);
        Span<byte> metaBytes = SpanHelpers.AsByteSpan(ref meta);

        Result rc = _storage.Get.Read(Package2Header.SignatureSize, metaBytes);
        if (rc.IsFailure()) return rc;

        return _header.VerifySignature(_keySet.Package2SigningKeyParams.Modulus, metaBytes);
    }

    /// <summary>
    /// Verifies the package metadata.
    /// </summary>
    /// <returns>The <see cref="Result"/> of the operation.
    /// <see cref="Result.Success"/> if the metadata is valid.</returns>
    public Result VerifyMeta() => _header.Meta.Verify();

    /// <summary>
    /// Verifies the hashes of all the payloads in the metadata.
    /// </summary>
    /// <returns>The <see cref="Result"/> of the operation.
    /// <see cref="Result.Success"/> if all the hashes are valid.</returns>
    public Result VerifyPayloads()
    {
        using (var buffer = new RentedArray<byte>(0x10000))
        {
            byte[] array = buffer.Array;
            var hashBuffer = new Buffer32();
            var sha = new Sha256Generator();

            // Verify hashes match for all payloads.
            for (int i = 0; i < Package2Header.PayloadCount; i++)
            {
                if (_header.Meta.PayloadSizes[i] == 0)
                    continue;

                int offset = _header.Meta.GetPayloadFileOffset(i);
                int size = (int)_header.Meta.PayloadSizes[i];

                var payloadSubStorage = new SubStorage(_storage, offset, size);

                offset = 0;
                sha.Initialize();

                while (size > 0)
                {
                    int toRead = Math.Min(array.Length, size);
                    Span<byte> span = array.AsSpan(0, toRead);

                    Result rc = payloadSubStorage.Read(offset, span);
                    if (rc.IsFailure()) return rc;

                    sha.Update(span);

                    offset += toRead;
                    size -= toRead;
                }

                sha.GetHash(hashBuffer);

                if (!CryptoUtil.IsSameBytes(hashBuffer, _header.Meta.PayloadHashes[i], 0x20))
                {
                    return ResultLibHac.InvalidPackage2PayloadCorrupted.Log();
                }
            }
        }

        return Result.Success;
    }

    /// <summary>
    /// Opens a decrypted <see cref="IStorage"/> of the entire package.
    /// </summary>
    /// <param name="outPackageStorage">If the method returns successfully, contains a decrypted
    /// <see cref="IStorage"/> of the package.</param>
    /// <returns>The <see cref="Result"/> of the operation.</returns>
    public Result OpenDecryptedPackage(ref UniqueRef<IStorage> outPackageStorage)
    {
        var storages = new List<IStorage>(4);

        // The signature and IV are unencrypted
        int unencryptedHeaderSize = Package2Header.SignatureSize + Unsafe.SizeOf<Buffer16>();
        int encryptedHeaderSize = Unsafe.SizeOf<Package2Header>() - unencryptedHeaderSize;

        // Get signature and IV
        storages.Add(new SubStorage(_storage, 0, unencryptedHeaderSize));

        // Open decrypted meta
        var encMetaStorage = new SubStorage(_storage, unencryptedHeaderSize, encryptedHeaderSize);

        // The counter starts counting at the beginning of the meta struct, but the first block in
        // the struct isn't encrypted. Increase the counter by one to skip that block.
        byte[] iv = _header.Meta.HeaderIv.Bytes.ToArray();
        Utilities.IncrementByteArray(iv);

        storages.Add(new CachedStorage(new Aes128CtrStorage(encMetaStorage, _key.DataRo.ToArray(), iv, true), 0x100, 1, true));

        // Open all the payloads
        for (int i = 0; i < Package2Header.PayloadCount; i++)
        {
            if (_header.Meta.PayloadSizes[i] == 0)
                continue;

            using var payloadStorage = new UniqueRef<IStorage>();
            Result rc = OpenPayload(ref payloadStorage.Ref(), i);
            if (rc.IsFailure()) return rc.Miss();

            storages.Add(payloadStorage.Release());
        }

        outPackageStorage.Reset(new ConcatenationStorage(storages, true));
        return Result.Success;
    }

    private void DecryptHeader(ReadOnlySpan<byte> key, ref Package2Meta source, ref Package2Meta dest)
    {
        Buffer16 iv = source.HeaderIv;

        Aes.DecryptCtr128(SpanHelpers.AsByteSpan(ref source), SpanHelpers.AsByteSpan(ref dest), key, iv);

        // Copy the IV to the output because the IV field will be garbage after "decrypting" it
        Unsafe.As<Package2Meta, Buffer16>(ref dest) = iv;
    }

    private bool HasIniPayload()
    {
        return _header.Meta.PayloadSizes[IniPayloadIndex] != 0;
    }
}
