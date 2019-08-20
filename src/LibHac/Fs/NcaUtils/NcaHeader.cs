using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibHac.Fs.NcaUtils
{
    public struct NcaHeader
    {
        internal const int HeaderSize = 0xC00;
        internal const int HeaderSectorSize = 0x200;
        internal const int BlockSize = 0x200;
        internal const int SectionCount = 4;

        private readonly Memory<byte> _header;

        public NcaHeader(IStorage headerStorage)
        {
            _header = new byte[HeaderSize];
            headerStorage.Read(_header.Span, 0);
        }

        public NcaHeader(Keyset keyset, IStorage headerStorage)
        {
            _header = DecryptHeader(keyset, headerStorage);
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

        public bool HasRightsId => !Util.IsEmpty(RightsId);

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

            int offset = NcaHeaderStruct.KeyAreaOffset + Crypto.Aes128Size * index;
            return _header.Span.Slice(offset, Crypto.Aes128Size);
        }

        public NcaFsHeader GetFsHeader(int index)
        {
            Span<byte> expectedHash = GetFsHeaderHash(index);

            int offset = NcaHeaderStruct.FsHeadersOffset + NcaHeaderStruct.FsHeaderSize * index;
            // ReSharper disable once ImpureMethodCallOnReadonlyValueField
            Memory<byte> headerData = _header.Slice(offset, NcaHeaderStruct.FsHeaderSize);

            byte[] actualHash = Crypto.ComputeSha256(headerData.ToArray(), 0, NcaHeaderStruct.FsHeaderSize);

            if (!Util.SpansEqual(expectedHash, actualHash))
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

        public static byte[] DecryptHeader(Keyset keyset, IStorage storage)
        {
            var buf = new byte[HeaderSize];
            storage.Read(buf, 0);

            byte[] key1 = keyset.HeaderKey.AsSpan(0, 0x10).ToArray();
            byte[] key2 = keyset.HeaderKey.AsSpan(0x10, 0x10).ToArray();

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
            else
            {
                throw new NotSupportedException($"NCA version {version} is not supported.");
            }

            return buf;
        }

        public Validity VerifySignature1(byte[] modulus)
        {
            return Crypto.Rsa2048PssVerify(_header.Span.Slice(0x200, 0x200).ToArray(), Signature1.ToArray(), modulus);
        }

        public Validity VerifySignature2(byte[] modulus)
        {
            return Crypto.Rsa2048PssVerify(_header.Span.Slice(0x200, 0x200).ToArray(), Signature2.ToArray(), modulus);
        }

        [StructLayout(LayoutKind.Explicit, Size = 0xC00)]
        private struct NcaHeaderStruct
        {
            public const int RightsIdOffset = 0x230;
            public const int RightsIdSize = 0x10;
            public const int SectionEntriesOffset = 0x240;
            public const int FsHeaderHashOffset = 0x280;
            public const int FsHeaderHashSize = 0x20;
            public const int KeyAreaOffset = 0x300;
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
}
