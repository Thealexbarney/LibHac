using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Crypto;
using LibHac.Util;

namespace LibHac.Boot;

public struct Package2Header
{
    internal const int Package2SizeMax = (1024 * 1024 * 8) - (1024 * 16); // 8MB - 16KB
    internal const int PayloadAlignment = 4;
    internal const int PayloadCount = 3;

    internal const int SignatureSize = 0x100;
    private static ReadOnlySpan<byte> RsaPublicKeyExponent => new byte[] { 0x00, 0x01, 0x00, 0x01 };

    public Array256<byte> Signature;
    public Package2Meta Meta;

    public readonly Result VerifySignature(ReadOnlySpan<byte> modulus, ReadOnlySpan<byte> data)
    {
        if (!Rsa.VerifyRsa2048PssSha256(Signature, modulus, RsaPublicKeyExponent, data))
        {
            return ResultLibHac.InvalidPackage2HeaderSignature.Log();
        }

        return Result.Success;
    }
}

public struct Package2Meta
{
    public static readonly uint ExpectedMagicValue = 0x31324B50; // PK21

    public Array16<byte> HeaderIv;
    public Array3<Array16<byte>> PayloadIvs;
    public Array16<byte> Padding40;

    public uint Magic;
    public uint EntryPoint;
    public Array4<byte> Padding58;
    public byte Package2Version;
    public byte BootloaderVersion;

    public Array3<uint> PayloadSizes;
    public Array4<byte> Padding6C;
    public Array3<uint> PayloadOffsets;
    public Array4<byte> Padding7C;
    public Array3<Array32<byte>> PayloadHashes;
    public Array32<byte> PaddingE0;

    public readonly uint GetSize()
    {
        ReadOnlySpan<uint> ints = SpanHelpers.AsReadOnlySpan<Array16<byte>, uint>(in HeaderIv);
        return ints[0] ^ ints[2] ^ ints[3];
    }

    public readonly byte GetKeyGeneration() => (byte)Math.Max(0, (HeaderIv[4] ^ HeaderIv[6] ^ HeaderIv[7]) - 1);

    public readonly int GetPayloadFileOffset(int index)
    {
        if ((uint)index >= Package2Header.PayloadCount)
            throw new IndexOutOfRangeException("Invalid payload index.");

        int offset = Unsafe.SizeOf<Package2Header>();

        for (int i = 0; i < index; i++)
        {
            offset += (int)PayloadSizes[i];
        }

        return offset;
    }

    public readonly Result Verify()
    {
        // Get the obfuscated metadata.
        uint size = GetSize();
        byte keyGeneration = GetKeyGeneration();

        // Check that size is big enough for the header.
        if (size < Unsafe.SizeOf<Package2Header>())
            return ResultLibHac.InvalidPackage2MetaSizeA.Log();

        // Check that the size isn't larger than what we allow.
        if (size > Package2Header.Package2SizeMax)
            return ResultLibHac.InvalidPackage2MetaSizeB.Log();

        // Check that the key generation is one that we can use.
        if (keyGeneration >= 0x20)
            return ResultLibHac.InvalidPackage2MetaKeyGeneration.Log();

        // Check the magic number.
        if (Magic != ExpectedMagicValue)
            return ResultLibHac.InvalidPackage2MetaMagic.Log();

        // Check the payload alignments.
        if (EntryPoint % Package2Header.PayloadAlignment != 0)
            return ResultLibHac.InvalidPackage2MetaEntryPointAlignment.Log();

        for (int i = 0; i < Package2Header.PayloadCount; i++)
        {
            if (PayloadSizes[i] % Package2Header.PayloadAlignment != 0)
                return ResultLibHac.InvalidPackage2MetaPayloadSizeAlignment.Log();
        }

        // Check that the sizes sum to the total.
        if (GetSize() != Unsafe.SizeOf<Package2Header>() + PayloadSizes[0] + PayloadSizes[1] + PayloadSizes[2])
            return ResultLibHac.InvalidPackage2MetaTotalSize.Log();

        // Check that the payloads do not overflow.
        for (int i = 0; i < Package2Header.PayloadCount; i++)
        {
            if (PayloadOffsets[i] > PayloadOffsets[i] + PayloadSizes[i])
                return ResultLibHac.InvalidPackage2MetaPayloadSize.Log();
        }

        // Verify that no payloads overlap.
        for (int i = 0; i < Package2Header.PayloadCount - 1; i++)
            for (int j = i + 1; j < Package2Header.PayloadCount; j++)
            {
                if (Overlap.HasOverlap(PayloadOffsets[i], PayloadSizes[i], PayloadOffsets[j], PayloadSizes[j]))
                    return ResultLibHac.InvalidPackage2MetaPayloadsOverlap.Log();
            }

        // Check whether any payload contains the entrypoint.
        for (int i = 0; i < Package2Header.PayloadCount; i++)
        {
            if (Overlap.Contains(PayloadOffsets[i], PayloadSizes[i], EntryPoint))
                return Result.Success;
        }

        // No payload contains the entrypoint, so we're not valid.
        return ResultLibHac.InvalidPackage2MetaEntryPointNotFound.Log();
    }
}