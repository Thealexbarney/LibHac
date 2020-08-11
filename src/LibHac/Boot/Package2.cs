using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Crypto;
using LibHac.Util;
#if DEBUG
using System.Diagnostics;
#endif

namespace LibHac.Boot
{
    [StructLayout(LayoutKind.Explicit, Size = 0x200)]
    public struct Package2Header
    {
        internal const int Package2SizeMax = (1024 * 1024 * 8) - (1024 * 16); // 8MB - 16KB
        internal const int PayloadAlignment = 4;
        internal const int PayloadCount = 3;

        internal const int SignatureSize = 0x100;
        private ReadOnlySpan<byte> RsaPublicKeyExponent => new byte[] { 0x00, 0x01, 0x00, 0x01 };

        [FieldOffset(0x00)] private byte _signature;
        [FieldOffset(0x100)] public Package2Meta Meta;

        public ReadOnlySpan<byte> Signature => SpanHelpers.CreateSpan(ref _signature, SignatureSize);

        public Result VerifySignature(ReadOnlySpan<byte> modulus, ReadOnlySpan<byte> data)
        {
            if (!Rsa.VerifyRsa2048PssSha256(Signature, modulus, RsaPublicKeyExponent, data))
            {
                return ResultLibHac.InvalidPackage2HeaderSignature.Log();
            }

            return Result.Success;
        }

#if DEBUG
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] [FieldOffset(0x00)] private readonly Padding100 PaddingForVsDebugging;
#endif
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x100)]
    public struct Package2Meta
    {
        public const uint ExpectedMagicValue = 0x31324B50; // PK21

        [FieldOffset(0x00)] private Buffer16 _headerIv;

        [FieldOffset(0x00)] private uint _package2Size;
        [FieldOffset(0x04)] private byte _keyGeneration;

        [FieldOffset(0x06)] private byte _keyGenerationXor1;
        [FieldOffset(0x07)] private byte _keyGenerationXor2;
        [FieldOffset(0x08)] private uint _sizeXor1;
        [FieldOffset(0x0C)] private uint _sizeXor2;

        [FieldOffset(0x10)] private Buffer16 _payloadIvs;

        [FieldOffset(0x50)] private readonly uint _magic;
        [FieldOffset(0x54)] private readonly uint _entryPoint;
        [FieldOffset(0x5C)] private readonly byte _package2Version;
        [FieldOffset(0x5D)] private readonly byte _bootloaderVersion;

        [FieldOffset(0x60)] private uint _payloadSizes;
        [FieldOffset(0x70)] private uint _payloadOffsets;
        [FieldOffset(0x80)] private Buffer32 _payloadHashes;

        public uint Magic => _magic;
        public uint EntryPoint => _entryPoint;
        public byte Package2Version => _package2Version;
        public byte BootloaderVersion => _bootloaderVersion;

        public Buffer16 HeaderIv => _headerIv;
        public readonly uint Size => _package2Size ^ _sizeXor1 ^ _sizeXor2;
        public byte KeyGeneration => (byte)Math.Max(0, (_keyGeneration ^ _keyGenerationXor1 ^ _keyGenerationXor2) - 1);

        public ReadOnlySpan<Buffer16> PayloadIvs => SpanHelpers.CreateSpan(ref _payloadIvs, Package2Header.PayloadCount);
        public ReadOnlySpan<uint> PayloadSizes => SpanHelpers.CreateSpan(ref _payloadSizes, Package2Header.PayloadCount);
        public ReadOnlySpan<uint> PayloadOffsets => SpanHelpers.CreateSpan(ref _payloadOffsets, Package2Header.PayloadCount);
        public ReadOnlySpan<Buffer32> PayloadHashes => SpanHelpers.CreateSpan(ref _payloadHashes, Package2Header.PayloadCount);

        public int GetPayloadFileOffset(int index)
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

        public Result Verify()
        {
            // Get the obfuscated metadata.
            uint size = Size;
            byte keyGeneration = KeyGeneration;

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
            if (Size != Unsafe.SizeOf<Package2Header>() + PayloadSizes[0] + PayloadSizes[1] + PayloadSizes[2])
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

#if DEBUG
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] [FieldOffset(0x00)] private readonly Padding100 PaddingForVsDebugging;
#endif
    }
}
