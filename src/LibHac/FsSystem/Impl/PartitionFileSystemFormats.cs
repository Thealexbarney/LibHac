using System;
using System.Runtime.InteropServices;
using LibHac.Common.FixedArrays;
using LibHac.Fs;

namespace LibHac.FsSystem.Impl;

public struct PartitionFileSystemFormat : IPartitionFileSystemFormat
{
    public static ReadOnlySpan<byte> VersionSignature => "PFS0"u8;
    public static uint EntryNameLengthMax => PathTool.EntryNameLengthMax;
    public static uint FileDataAlignmentSize => 0x20;
    public static Result ResultSignatureVerificationFailed => ResultFs.PartitionSignatureVerificationFailed.Value;

    [StructLayout(LayoutKind.Sequential)]
    public struct PartitionEntry : IPartitionFileSystemEntry
    {
        public long Offset;
        public long Size;
        public int NameOffset;
        public uint Reserved;

        readonly long IPartitionFileSystemEntry.Offset => Offset;
        readonly long IPartitionFileSystemEntry.Size => Size;
        readonly int IPartitionFileSystemEntry.NameOffset => NameOffset;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PartitionFileSystemHeaderImpl : IPartitionFileSystemHeader
    {
        private Array4<byte> _signature;
        public int EntryCount;
        public int NameTableSize;
        public uint Reserved;

        public readonly ReadOnlySpan<byte> Signature
        {
            get
            {
                ReadOnlySpan<byte> span = _signature.ItemsRo;
                return MemoryMarshal.CreateReadOnlySpan(ref MemoryMarshal.GetReference(span), span.Length);
            }
        }

        readonly int IPartitionFileSystemHeader.EntryCount => EntryCount;
        readonly int IPartitionFileSystemHeader.NameTableSize => NameTableSize;
    }
}

public struct Sha256PartitionFileSystemFormat : IPartitionFileSystemFormat
{
    public static ReadOnlySpan<byte> VersionSignature => "HFS0"u8;
    public static uint EntryNameLengthMax => PathTool.EntryNameLengthMax;
    public static uint FileDataAlignmentSize => 0x200;
    public static Result ResultSignatureVerificationFailed => ResultFs.Sha256PartitionSignatureVerificationFailed.Value;

    [StructLayout(LayoutKind.Sequential)]
    public struct PartitionEntry : IPartitionFileSystemEntry
    {
        public long Offset;
        public long Size;
        public int NameOffset;
        public int HashTargetSize;
        public long HashTargetOffset;
        public Array32<byte> Hash;

        readonly long IPartitionFileSystemEntry.Offset => Offset;
        readonly long IPartitionFileSystemEntry.Size => Size;
        readonly int IPartitionFileSystemEntry.NameOffset => NameOffset;
    }
}