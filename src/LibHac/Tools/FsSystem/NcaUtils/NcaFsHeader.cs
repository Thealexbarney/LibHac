using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.FsSystem;

// ReSharper disable ImpureMethodCallOnReadonlyValueField

namespace LibHac.Tools.FsSystem.NcaUtils;

public struct NcaFsHeader
{
    private readonly Memory<byte> _header;

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
        return new NcaFsIntegrityInfoIvfc(_header.Slice(FsHeaderStruct.IntegrityInfoOffset, FsHeaderStruct.IntegrityInfoSize));
    }

    public NcaFsIntegrityInfoSha256 GetIntegrityInfoSha256()
    {
        return new NcaFsIntegrityInfoSha256(_header.Slice(FsHeaderStruct.IntegrityInfoOffset, FsHeaderStruct.IntegrityInfoSize));
    }

    public NcaFsPatchInfo GetPatchInfo()
    {
        return new NcaFsPatchInfo(_header.Slice(FsHeaderStruct.PatchInfoOffset, FsHeaderStruct.PatchInfoSize));
    }

    public bool IsPatchSection()
    {
        return GetPatchInfo().RelocationTreeSize != 0;
    }

    public ref NcaSparseInfo GetSparseInfo()
    {
        return ref MemoryMarshal.Cast<byte, NcaSparseInfo>(_header.Span.Slice(FsHeaderStruct.SparseInfoOffset,
            FsHeaderStruct.SparseInfoSize))[0];
    }

    public bool ExistsSparseLayer()
    {
        return GetSparseInfo().Generation != 0;
    }

    public ref NcaCompressionInfo GetCompressionInfo()
    {
        return ref MemoryMarshal.Cast<byte, NcaCompressionInfo>(_header.Span.Slice(FsHeaderStruct.CompressionInfoOffset,
            FsHeaderStruct.CompressionInfoSize))[0];
    }

    public bool ExistsCompressionLayer()
    {
        return GetCompressionInfo().MetaOffset != 0 && GetCompressionInfo().MetaSize != 0;
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
        public const int IntegrityInfoOffset = 8;
        public const int IntegrityInfoSize = 0xF8;
        public const int PatchInfoOffset = 0x100;
        public const int PatchInfoSize = 0x40;
        public const int SparseInfoOffset = 0x148;
        public const int SparseInfoSize = 0x30;
        public const int CompressionInfoOffset = 0x178;
        public const int CompressionInfoSize = 0x20;

        [FieldOffset(0)] public short Version;
        [FieldOffset(2)] public byte FormatType;
        [FieldOffset(3)] public byte HashType;
        [FieldOffset(4)] public byte EncryptionType;
        [FieldOffset(0x140)] public ulong UpperCounter;
        [FieldOffset(0x140)] public int CounterType;
        [FieldOffset(0x144)] public int CounterVersion;
    }
}