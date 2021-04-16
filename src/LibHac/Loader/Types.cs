using System;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Ncm;
#pragma warning disable 169 // Unused private fields

namespace LibHac.Loader
{
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
        private Array4<byte> _reserved08;
        public byte Flags;
        private byte _reserved0D;
        public byte MainThreadPriority;
        public byte DefaultCpuId;
        private Array4<byte> _reserved10;
        public uint SystemResourceSize;
        public uint Version;
        public uint MainThreadStackSize;
        private Array16<byte> _programName;
        private Array16<byte> _productCode;
        private Array32<byte> _reserved40;
        private Array16<byte> _reserved60;
        public int AciOffset;
        public int AciSize;
        public int AcidOffset;
        public int AcidSize;

        public readonly ReadOnlySpan<byte> ProgramName => _programName.ItemsRo;
        public readonly ReadOnlySpan<byte> ProductCode => _productCode.ItemsRo;
    }

    public struct AciHeader
    {
        public static readonly uint MagicValue = 0x30494341; // ACI0

        public uint Magic;
        private Array12<byte> _reserved04;
        public ProgramId ProgramId;
        private Array8<byte> _reserved18;
        public int FsAccessControlOffset;
        public int FsAccessControlSize;
        public int ServiceAccessControlOffset;
        public int ServiceAccessControlSize;
        public int KernelCapabilityOffset;
        public int KernelCapabilitySize;
        private Array4<byte> _reserved38;
    }

    public struct AcidHeaderData
    {
        public static readonly uint MagicValue = 0x44494341; // ACID

        private Array256<byte> _signature;
        private Array256<byte> _modulus;
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
        private Array4<byte> _reserved238;

        public readonly ReadOnlySpan<byte> Signature => _signature.ItemsRo;
        public readonly ReadOnlySpan<byte> Modulus => _modulus.ItemsRo;
    }
}
