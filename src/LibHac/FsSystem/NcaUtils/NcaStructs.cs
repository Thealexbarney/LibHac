using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common.FixedArrays;

namespace LibHac.FsSystem.NcaUtils;

public class TitleVersion
{
    public uint Version { get; }
    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }
    public int Revision { get; }

    public TitleVersion(uint version, bool isSystemTitle = false)
    {
        Version = version;

        if (isSystemTitle)
        {
            Revision = (int)(version & ((1 << 16) - 1));
            Patch = (int)((version >> 16) & ((1 << 4) - 1));
            Minor = (int)((version >> 20) & ((1 << 6) - 1));
            Major = (int)((version >> 26) & ((1 << 6) - 1));
        }
        else
        {
            Revision = (byte)version;
            Patch = (byte)(version >> 8);
            Minor = (byte)(version >> 16);
            Major = (byte)(version >> 24);
        }
    }

    public override string ToString()
    {
        return $"{Major}.{Minor}.{Patch}.{Revision}";
    }
}

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
