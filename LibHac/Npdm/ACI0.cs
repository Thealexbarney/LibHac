using System;
using System.IO;

namespace LibHac.Npdm
{
    public class ACI0
    {
        public string Magic;

        public long TitleId { get; private set; }

        public int   FsVersion            { get; private set; }
        public ulong FsPermissionsBitmask { get; private set; }

        public ServiceAccessControl ServiceAccess{ get; private set; }
        public KernelAccessControl  KernelAccess  { get; private set; }

        public ACI0(Stream Stream, int Offset)
        {
            Stream.Seek(Offset, SeekOrigin.Begin);

            BinaryReader Reader = new BinaryReader(Stream);

            Magic = Reader.ReadAscii(0x4);

            if (Magic != "ACI0")
            {
                throw new Exception("ACI0 Stream doesn't contain ACI0 section!");
            }

            Stream.Seek(0xc, SeekOrigin.Current);

            TitleId = Reader.ReadInt64();

            //Reserved.
            Stream.Seek(8, SeekOrigin.Current);

            int FsAccessHeaderOffset       = Reader.ReadInt32();
            int FsAccessHeaderSize         = Reader.ReadInt32();
            int ServiceAccessControlOffset = Reader.ReadInt32();
            int ServiceAccessControlSize   = Reader.ReadInt32();
            int KernelAccessControlOffset  = Reader.ReadInt32();
            int KernelAccessControlSize    = Reader.ReadInt32();

            FsAccessHeader AccessHeader = new FsAccessHeader(Stream, Offset + FsAccessHeaderOffset, FsAccessHeaderSize);

            FsVersion            = AccessHeader.Version;
            FsPermissionsBitmask = AccessHeader.PermissionsBitmask;

            ServiceAccess = new ServiceAccessControl(Stream, Offset + ServiceAccessControlOffset, ServiceAccessControlSize);

            KernelAccess = new KernelAccessControl(Stream, Offset + KernelAccessControlOffset, KernelAccessControlSize);
        }
    }
}
