using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common.FixedArrays;

namespace LibHac.FsSystem;

public struct NcaSparseInfo
{
    public long MetaOffset;
    public long MetaSize;
    public Array16<byte> MetaHeader;
    public long PhysicalOffset;
    public ushort Generation;
    private Array6<byte> _reserved;

    public readonly uint GetGeneration() => (uint)(Generation << 16);
    public readonly long GetPhysicalSize() => MetaOffset + MetaSize;

    public readonly NcaAesCtrUpperIv MakeAesCtrUpperIv(NcaAesCtrUpperIv upperIv)
    {
        NcaAesCtrUpperIv sparseUpperIv = upperIv;
        sparseUpperIv.Generation = GetGeneration();
        return sparseUpperIv;
    }
}

[StructLayout(LayoutKind.Explicit)]
public struct NcaAesCtrUpperIv
{
    [FieldOffset(0)] public ulong Value;

    [FieldOffset(0)] public uint Generation;
    [FieldOffset(4)] public uint SecureValue;

    internal NcaAesCtrUpperIv(ulong value)
    {
        Unsafe.SkipInit(out Generation);
        Unsafe.SkipInit(out SecureValue);
        Value = value;
    }
}

public enum NcaSectionType
{
    Code,
    Data,
    Logo
}

public enum NcaContentType
{
    Program,
    Meta,
    Control,
    Manual,
    Data,
    PublicData
}

public enum DistributionType
{
    Download,
    GameCard
}

public enum NcaEncryptionType
{
    Auto,
    None,
    XTS,
    AesCtr,
    AesCtrEx
}

public enum NcaHashType
{
    Auto,
    None,
    Sha256,
    Ivfc
}

public enum NcaFormatType
{
    Romfs,
    Pfs0
}