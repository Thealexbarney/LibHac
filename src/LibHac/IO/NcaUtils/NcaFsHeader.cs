using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibHac.IO.NcaUtils
{
    public class NcaFsHeader
    {
        private const int IntegrityInfoOffset = 8;
        private const int IntegrityInfoSize = 0xF8;
        private const int PatchInfoOffset = 0x100;
        private const int PatchInfoSize = 0x40;

        private Memory<byte> _header;

        public NcaFsHeader(Memory<byte> headerData)
        {
            _header = headerData;
        }

        private ref FsHeaderStruct Header => ref Unsafe.As<byte, FsHeaderStruct>(ref _header.Span[0]);

        public short Version
        {
            get => Header.Version;
            set => Header.Version = value;
        }

        public NcaFormatType FormatType
        {
            get => (NcaFormatType)Header.FormatType;
            set => Header.FormatType = (byte)value;
        }

        public NcaHashType HashType
        {
            get => (NcaHashType)Header.HashType;
            set => Header.HashType = (byte)value;
        }

        public NcaEncryptionType EncryptionType
        {
            get => (NcaEncryptionType)Header.EncryptionType;
            set => Header.EncryptionType = (byte)value;
        }

        public NcaFsIntegrityInfoIvfc GetIntegrityInfoIvfc()
        {
            return new NcaFsIntegrityInfoIvfc(_header.Slice(IntegrityInfoOffset, IntegrityInfoSize));
        }

        public NcaFsIntegrityInfoSha256 GetIntegrityInfoSha256()
        {
            return new NcaFsIntegrityInfoSha256(_header.Slice(IntegrityInfoOffset, IntegrityInfoSize));
        }

        public NcaFsPatchInfo GetPatchInfo()
        {
            return new NcaFsPatchInfo(_header.Slice(PatchInfoOffset, PatchInfoSize));
        }

        public bool IsPatchSection()
        {
            return GetPatchInfo().RelocationTreeSize != 0;
        }

        public ulong Counter
        {
            get => Header.UpperCounter;
            set => Header.UpperCounter = value;
        }

        public int CounterType
        {
            get => Header.CounterType;
            set => Header.CounterType = value;
        }

        public int CounterVersion
        {
            get => Header.CounterVersion;
            set => Header.CounterVersion = value;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct FsHeaderStruct
        {
            [FieldOffset(0)] public short Version;
            [FieldOffset(2)] public byte FormatType;
            [FieldOffset(3)] public byte HashType;
            [FieldOffset(4)] public byte EncryptionType;
            [FieldOffset(0x140)] public ulong UpperCounter;
            [FieldOffset(0x140)] public int CounterType;
            [FieldOffset(0x144)] public int CounterVersion;
        }
    }
}
