using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Crypto;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Util;

namespace LibHac.FsSystem.NcaUtils
{
    public struct NcaHeader
    {
        internal const int HeaderSize = 0xC00;
        internal const int HeaderSectorSize = 0x200;
        internal const int BlockSize = 0x200;
        internal const int SectionCount = 4;

        private readonly Memory<byte> _header;

        public NcaVersion FormatVersion { get; }
        public bool IsEncrypted { get; }

        public NcaHeader(KeySet keySet, IStorage headerStorage)
        {
            (byte[] header, bool isEncrypted) = DecryptHeader(keySet, headerStorage);

            _header = header;
            IsEncrypted = isEncrypted;
            FormatVersion = DetectNcaVersion(_header.Span);
        }

        private ref NcaHeaderStruct Header => ref Unsafe.As<byte, NcaHeaderStruct>(ref _header.Span[0]);

        public Span<byte> Signature1 => _header.Span.Slice(0, 0x100);
        public Span<byte> Signature2 => _header.Span.Slice(0x100, 0x100);

        public uint Magic
        {
            get => Header.Magic;
            set => Header.Magic = value;
        }

        public int Version => _header.Span[0x203] - '0';

        public DistributionType DistributionType
        {
            get => (DistributionType)Header.DistributionType;
            set => Header.DistributionType = (byte)value;
        }

        public NcaContentType ContentType
        {
            get => (NcaContentType)Header.ContentType;
            set => Header.ContentType = (byte)value;
        }

        public byte KeyGeneration
        {
            get => Math.Max(Header.KeyGeneration1, Header.KeyGeneration2);
            set
            {
                if (value > 2)
                {
                    Header.KeyGeneration1 = 2;
                    Header.KeyGeneration2 = value;
                }
                else
                {
                    Header.KeyGeneration1 = value;
                    Header.KeyGeneration2 = 0;
                }
            }
        }

        public byte KeyAreaKeyIndex
        {
            get => Header.KeyAreaKeyIndex;
            set => Header.KeyAreaKeyIndex = value;
        }

        public long NcaSize
        {
            get => Header.NcaSize;
            set => Header.NcaSize = value;
        }

        public ulong TitleId
        {
            get => Header.TitleId;
            set => Header.TitleId = value;
        }

        public int ContentIndex
        {
            get => Header.ContentIndex;
            set => Header.ContentIndex = value;
        }

        public TitleVersion SdkVersion
        {
            get => new TitleVersion(Header.SdkVersion);
            set => Header.SdkVersion = value.Version;
        }

        public Span<byte> RightsId => _header.Span.Slice(NcaHeaderStruct.RightsIdOffset, NcaHeaderStruct.RightsIdSize);

        public bool HasRightsId => !Utilities.IsZeros(RightsId);

        public Span<byte> GetKeyArea()
        {
            return _header.Span.Slice(NcaHeaderStruct.KeyAreaOffset, NcaHeaderStruct.KeyAreaSize);
        }

        private ref NcaSectionEntryStruct GetSectionEntry(int index)
        {
            ValidateSectionIndex(index);

            int offset = NcaHeaderStruct.SectionEntriesOffset + NcaSectionEntryStruct.SectionEntrySize * index;
            return ref Unsafe.As<byte, NcaSectionEntryStruct>(ref _header.Span[offset]);
        }

        public long GetSectionStartOffset(int index)
        {
            return BlockToOffset(GetSectionEntry(index).StartBlock);
        }

        public long GetSectionEndOffset(int index)
        {
            return BlockToOffset(GetSectionEntry(index).EndBlock);
        }

        public long GetSectionSize(int index)
        {
            ref NcaSectionEntryStruct info = ref GetSectionEntry(index);
            return BlockToOffset(info.EndBlock - info.StartBlock);
        }

        public bool IsSectionEnabled(int index)
        {
            ref NcaSectionEntryStruct info = ref GetSectionEntry(index);

            int sectStart = info.StartBlock;
            int sectSize = info.EndBlock - sectStart;
            return sectStart != 0 || sectSize != 0;
        }

        public Span<byte> GetFsHeaderHash(int index)
        {
            ValidateSectionIndex(index);

            int offset = NcaHeaderStruct.FsHeaderHashOffset + NcaHeaderStruct.FsHeaderHashSize * index;
            return _header.Span.Slice(offset, NcaHeaderStruct.FsHeaderHashSize);
        }

        public Span<byte> GetEncryptedKey(int index)
        {
            if (index < 0 || index >= SectionCount)
            {
                throw new ArgumentOutOfRangeException($"Key index must be between 0 and 3. Actual: {index}");
            }

            int offset = NcaHeaderStruct.KeyAreaOffset + Aes.KeySize128 * index;
            return _header.Span.Slice(offset, Aes.KeySize128);
        }

        public NcaFsHeader GetFsHeader(int index)
        {
            Span<byte> expectedHash = GetFsHeaderHash(index);

            int offset = NcaHeaderStruct.FsHeadersOffset + NcaHeaderStruct.FsHeaderSize * index;
            Memory<byte> headerData = _header.Slice(offset, NcaHeaderStruct.FsHeaderSize);

            Span<byte> actualHash = stackalloc byte[Sha256.DigestSize];
            Sha256.GenerateSha256Hash(headerData.Span, actualHash);

            if (!Utilities.SpansEqual(expectedHash, actualHash))
            {
                throw new InvalidDataException("FS header hash is invalid.");
            }

            return new NcaFsHeader(headerData);
        }

        private static void ValidateSectionIndex(int index)
        {
            if (index < 0 || index >= SectionCount)
            {
                throw new ArgumentOutOfRangeException($"NCA section index must be between 0 and 3. Actual: {index}");
            }
        }

        private static long BlockToOffset(int blockIndex)
        {
            return (long)blockIndex * BlockSize;
        }

        private static (byte[] header, bool isEncrypted) DecryptHeader(KeySet keySet, IStorage storage)
        {
            byte[] buf = new byte[HeaderSize];
            storage.Read(0, buf).ThrowIfFailure();

            if (CheckIfDecrypted(buf))
            {
                int decVersion = buf[0x203] - '0';

                if (decVersion != 0 && decVersion != 2 && decVersion != 3)
                {
                    throw new NotSupportedException($"NCA version {decVersion} is not supported.");
                }

                return (buf, false);
            }

            byte[] key1 = keySet.HeaderKey.SubKeys[0].DataRo.ToArray();
            byte[] key2 = keySet.HeaderKey.SubKeys[1].DataRo.ToArray();

            var transform = new Aes128XtsTransform(key1, key2, true);

            transform.TransformBlock(buf, HeaderSectorSize * 0, HeaderSectorSize, 0);
            transform.TransformBlock(buf, HeaderSectorSize * 1, HeaderSectorSize, 1);

            if (buf[0x200] != 'N' || buf[0x201] != 'C' || buf[0x202] != 'A')
            {
                throw new InvalidDataException(
                    "Unable to decrypt NCA header. The file is not an NCA file or the header key is incorrect.");
            }

            int version = buf[0x203] - '0';

            if (version == 3)
            {
                for (int sector = 2; sector < HeaderSize / HeaderSectorSize; sector++)
                {
                    transform.TransformBlock(buf, sector * HeaderSectorSize, HeaderSectorSize, (ulong)sector);
                }
            }
            else if (version == 2)
            {
                for (int i = 0x400; i < HeaderSize; i += HeaderSectorSize)
                {
                    transform.TransformBlock(buf, i, HeaderSectorSize, 0);
                }
            }
            else if (version != 0)
            {
                throw new NotSupportedException($"NCA version {version} is not supported.");
            }

            return (buf, true);
        }

        private static bool CheckIfDecrypted(ReadOnlySpan<byte> header)
        {
            Assert.SdkRequiresGreaterEqual(header.Length, 0x400);

            // Check the magic value
            if (header[0x200] != 'N' || header[0x201] != 'C' || header[0x202] != 'A')
                return false;

            // Check the version in the magic value
            if (!StringUtils.IsDigit(header[0x203]))
                return false;

            // Is the distribution type valid?
            if (header[0x204] > (int)DistributionType.GameCard)
                return false;

            // Is the content type valid?
            if (header[0x205] > (int)NcaContentType.PublicData)
                return false;

            return true;
        }

        private static NcaVersion DetectNcaVersion(ReadOnlySpan<byte> header)
        {
            int version = header[0x203] - '0';

            if (version == 3) return NcaVersion.Nca3;
            if (version == 2) return NcaVersion.Nca2;
            if (version != 0) return NcaVersion.Unknown;

            // There are multiple versions of NCA0 that each encrypt the key area differently.
            // Examine the key area to detect which version this NCA is.
            ReadOnlySpan<byte> keyArea = header.Slice(NcaHeaderStruct.KeyAreaOffset, NcaHeaderStruct.KeyAreaSize);

            // The end of the key area will only be non-zero if it's RSA-OAEP encrypted
            var zeros = new Buffer16();
            if (!keyArea.Slice(0x80, 0x10).SequenceEqual(zeros))
            {
                return NcaVersion.Nca0RsaOaep;
            }

            // Key areas using fixed, unencrypted keys always use the same keys.
            // Check for these keys by comparing the key area with the known hash of the fixed body keys.
            Unsafe.SkipInit(out Buffer32 hash);
            Sha256.GenerateSha256Hash(keyArea.Slice(0, 0x20), hash);

            if (Nca0FixedBodyKeySha256Hash.SequenceEqual(hash))
            {
                return NcaVersion.Nca0FixedKey;
            }

            // Otherwise the key area is encrypted the same as modern NCAs.
            return NcaVersion.Nca0;
        }

        public Validity VerifySignature1(byte[] modulus)
        {
            return CryptoOld.Rsa2048PssVerify(_header.Span.Slice(0x200, 0x200).ToArray(), Signature1.ToArray(), modulus);
        }

        public Validity VerifySignature2(byte[] modulus)
        {
            return CryptoOld.Rsa2048PssVerify(_header.Span.Slice(0x200, 0x200).ToArray(), Signature2.ToArray(), modulus);
        }

        public bool IsNca0() => FormatVersion >= NcaVersion.Nca0;

        private static ReadOnlySpan<byte> Nca0FixedBodyKeySha256Hash => new byte[]
        {
            0x9A, 0xBB, 0xD2, 0x11, 0x86, 0x00, 0x21, 0x9D, 0x7A, 0xDC, 0x5B, 0x43, 0x95, 0xF8, 0x4E, 0xFD,
            0xFF, 0x6B, 0x25, 0xEF, 0x9F, 0x96, 0x85, 0x28, 0x18, 0x9E, 0x76, 0xB0, 0x92, 0xF0, 0x6A, 0xCB
        };

        [StructLayout(LayoutKind.Explicit, Size = 0xC00)]
        private struct NcaHeaderStruct
        {
            public const int RightsIdOffset = 0x230;
            public const int RightsIdSize = 0x10;
            public const int SectionEntriesOffset = 0x240;
            public const int FsHeaderHashOffset = 0x280;
            public const int FsHeaderHashSize = 0x20;
            public const int KeyAreaOffset = 0x300;
            public const int KeyAreaSize = 0x100;
            public const int FsHeadersOffset = 0x400;
            public const int FsHeaderSize = 0x200;

            [FieldOffset(0x000)] public byte Signature1;
            [FieldOffset(0x100)] public byte Signature2;
            [FieldOffset(0x200)] public uint Magic;
            [FieldOffset(0x204)] public byte DistributionType;
            [FieldOffset(0x205)] public byte ContentType;
            [FieldOffset(0x206)] public byte KeyGeneration1;
            [FieldOffset(0x207)] public byte KeyAreaKeyIndex;
            [FieldOffset(0x208)] public long NcaSize;
            [FieldOffset(0x210)] public ulong TitleId;
            [FieldOffset(0x218)] public int ContentIndex;
            [FieldOffset(0x21C)] public uint SdkVersion;
            [FieldOffset(0x220)] public byte KeyGeneration2;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = SectionEntrySize)]
        private struct NcaSectionEntryStruct
        {
            public const int SectionEntrySize = 0x10;

            public int StartBlock;
            public int EndBlock;
            public bool IsEnabled;
        }
    }

    public enum NcaVersion
    {
        Unknown,
        Nca3,
        Nca2,
        Nca0,
        Nca0FixedKey,
        Nca0RsaOaep
    }
}
