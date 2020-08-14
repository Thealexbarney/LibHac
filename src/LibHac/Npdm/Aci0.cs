// ReSharper disable UnusedVariable
using System;
using System.IO;

namespace LibHac.Npdm
{
    public class Aci0
    {
        public string Magic;
        public long TitleId { get; }
        public int FsVersion { get; }
        public ulong FsPermissionsBitmask { get; }
        public ServiceAccessControl ServiceAccess { get; }
        public KernelAccessControl KernelAccess { get; }

        public Aci0(Stream stream, int offset)
        {
            stream.Seek(offset, SeekOrigin.Begin);

            var reader = new BinaryReader(stream);

            Magic = reader.ReadAscii(0x4);

            if (Magic != "ACI0")
            {
                throw new Exception("ACI0 Stream doesn't contain ACI0 section!");
            }

            stream.Seek(0xc, SeekOrigin.Current);

            TitleId = reader.ReadInt64();

            //Reserved.
            stream.Seek(8, SeekOrigin.Current);

            int fsAccessHeaderOffset = reader.ReadInt32();
            int fsAccessHeaderSize = reader.ReadInt32();
            int serviceAccessControlOffset = reader.ReadInt32();
            int serviceAccessControlSize = reader.ReadInt32();
            int kernelAccessControlOffset = reader.ReadInt32();
            int kernelAccessControlSize = reader.ReadInt32();

            if (fsAccessHeaderSize > 0)
            {
                var accessHeader = new FsAccessHeader(stream, offset + fsAccessHeaderOffset);

                FsVersion = accessHeader.Version;
                FsPermissionsBitmask = accessHeader.PermissionsBitmask;
            }

            if (serviceAccessControlSize > 0)
            {
                ServiceAccess = new ServiceAccessControl(stream, offset + serviceAccessControlOffset,
                    serviceAccessControlSize);
            }

            if (kernelAccessControlSize > 0)
            {
                KernelAccess =
                    new KernelAccessControl(stream, offset + kernelAccessControlOffset, kernelAccessControlSize);
            }
        }
    }
}
