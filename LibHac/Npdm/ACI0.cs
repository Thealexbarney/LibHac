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

        public ACI0(Stream stream, int offset)
        {
            stream.Seek(offset, SeekOrigin.Begin);

            BinaryReader reader = new BinaryReader(stream);

            Magic = reader.ReadAscii(0x4);

            if (Magic != "ACI0")
            {
                throw new Exception("ACI0 Stream doesn't contain ACI0 section!");
            }

            stream.Seek(0xc, SeekOrigin.Current);

            TitleId = reader.ReadInt64();

            //Reserved.
            stream.Seek(8, SeekOrigin.Current);

            int FsAccessHeaderOffset       = reader.ReadInt32();
            int FsAccessHeaderSize         = reader.ReadInt32();
            int ServiceAccessControlOffset = reader.ReadInt32();
            int ServiceAccessControlSize   = reader.ReadInt32();
            int KernelAccessControlOffset  = reader.ReadInt32();
            int KernelAccessControlSize    = reader.ReadInt32();

            FsAccessHeader AccessHeader = new FsAccessHeader(stream, offset + FsAccessHeaderOffset, FsAccessHeaderSize);

            FsVersion            = AccessHeader.Version;
            FsPermissionsBitmask = AccessHeader.PermissionsBitmask;

            ServiceAccess = new ServiceAccessControl(stream, offset + ServiceAccessControlOffset, ServiceAccessControlSize);

            KernelAccess = new KernelAccessControl(stream, offset + KernelAccessControlOffset, KernelAccessControlSize);
        }
    }
}
