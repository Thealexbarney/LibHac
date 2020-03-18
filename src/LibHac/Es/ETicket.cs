using System;
using System.Runtime.InteropServices;
using LibHac.Common;

namespace LibHac.Es
{
    [StructLayout(LayoutKind.Explicit, Size = 0x2C0)]
    public struct ETicket
    {
        private const int SignatureSize = 0x200;
        private const int IssuerSize = 0x40;
        private const int TitleKeySize = 0x100;
        private const int RightsIdSize = 0x10;

        [FieldOffset(0)] private SignatureType SignatureType;
        [FieldOffset(4)] private byte _signature;

        [FieldOffset(0x140)] private byte _issuer;
        [FieldOffset(0x180)] private byte _titleKey;

        [FieldOffset(0x280)] public byte FormatVersion;
        [FieldOffset(0x281)] public TitleKeyType TitleKeyType;
        [FieldOffset(0x282)] public ushort TicketVersion;
        [FieldOffset(0x284)] public LicenseType LicenseType;
        [FieldOffset(0x285)] public byte CommonKeyId;
        [FieldOffset(0x286)] public TicketProperties PropertyMask;

        [FieldOffset(0x290)] public ulong TicketId;
        [FieldOffset(0x298)] public ulong DeviceId;
        [FieldOffset(0x2A0)] private byte _rightsId;
        [FieldOffset(0x2B0)] public uint AccountId;
        [FieldOffset(0x2B4)] public int SectTotalSize;
        [FieldOffset(0x2B8)] public int SectHeaderOffset;
        [FieldOffset(0x2BC)] public short SectNum;
        [FieldOffset(0x2BE)] public short SectEntrySize;

        public Span<byte> Signature => SpanHelpers.CreateSpan(ref _signature, SignatureSize);
        public Span<byte> Issuer => SpanHelpers.CreateSpan(ref _issuer, IssuerSize);
        public Span<byte> TitleKey => SpanHelpers.CreateSpan(ref _titleKey, TitleKeySize);
        public Span<byte> RightsId => SpanHelpers.CreateSpan(ref _rightsId, RightsIdSize);
    }

    public enum SignatureType
    {
        Rsa4096Sha1 = 0x10000,
        Rsa2048Sha1 = 0x10001,
        EcdsaSha1 = 0x10002,
        Rsa4096Sha256 = 0x10003,
        Rsa2048Sha256 = 0x10004,
        EcdsaSha256 = 0x10005
    }

    public enum TitleKeyType : byte
    {
        Common = 0,
        Personalized = 1
    }

    public enum LicenseType : byte
    {
        Permanent = 0,
        Demo = 1,
        Trial = 2,
        Rental = 3,
        Subscription = 4,
        Service = 5
    }

    [Flags]
    public enum TicketProperties : byte
    {
        PreInstall = 1 << 0,
        SharedTitle = 1 << 1,
        AllowAllContent = 1 << 2
    }
}
