using System;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Ncm;
#pragma warning disable 169 // Unused private fields

namespace LibHac.Loader;

public ref struct Npdm
{
    public ReadOnlyRef<Meta> Meta;
    public ReadOnlyRef<AcidHeaderData> Acid;
    public ReadOnlyRef<AciHeader> Aci;

    public ReadOnlySpan<byte> FsAccessControlDescriptor;
    public ReadOnlySpan<byte> ServiceAccessControlDescriptor;
    public ReadOnlySpan<byte> KernelCapabilityDescriptor;

    public ReadOnlySpan<byte> FsAccessControlData;
    public ReadOnlySpan<byte> ServiceAccessControlData;
    public ReadOnlySpan<byte> KernelCapabilityData;
}

public struct Meta
{
    public static readonly uint MagicValue = 0x4154454D; // META

    public uint Magic;
    public int SignatureKeyGeneration;
    public Array4<byte> Reserved08;
    public byte Flags;
    public byte Reserved0D;
    public byte MainThreadPriority;
    public byte DefaultCpuId;
    public Array4<byte> Reserved10;
    public uint SystemResourceSize;
    public uint Version;
    public uint MainThreadStackSize;
    public Array16<byte> ProgramName;
    public Array16<byte> ProductCode;
    public Array48<byte> Reserved40;
    public int AciOffset;
    public int AciSize;
    public int AcidOffset;
    public int AcidSize;
}

public struct AciHeader
{
    public static readonly uint MagicValue = 0x30494341; // ACI0

    public uint Magic;
    public Array12<byte> Reserved04;
    public ProgramId ProgramId;
    public Array8<byte> Reserved18;
    public int FsAccessControlOffset;
    public int FsAccessControlSize;
    public int ServiceAccessControlOffset;
    public int ServiceAccessControlSize;
    public int KernelCapabilityOffset;
    public int KernelCapabilitySize;
    public Array8<byte> Reserved38;
}

public struct AcidHeaderData
{
    public static readonly uint MagicValue = 0x44494341; // ACID

    public Array256<byte> Signature;
    public Array256<byte> Modulus;
    public uint Magic;
    public int Size;
    public byte Version;
    public uint Flags;
    public ProgramId ProgramIdMin;
    public ProgramId ProgramIdMax;
    public int FsAccessControlOffset;
    public int FsAccessControlSize;
    public int ServiceAccessControlOffset;
    public int ServiceAccessControlSize;
    public int KernelCapabilityOffset;
    public int KernelCapabilitySize;
    public Array4<byte> Reserved238;
}