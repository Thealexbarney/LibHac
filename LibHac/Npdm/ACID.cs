using System;
using System.IO;

namespace LibHac.Npdm
{
    public class ACID
    {

        public string Magic;
        public byte[] RSA2048Signature { get; private set; }
        public byte[] RSA2048Modulus   { get; private set; }
        public int    Unknown1         { get; private set; }
        public int    Flags            { get; private set; }

        public long TitleIdRangeMin { get; private set; }
        public long TitleIdRangeMax { get; private set; }

        public FsAccessControl      FsAccess      { get; private set; }
        public ServiceAccessControl ServiceAccess { get; private set; }
        public KernelAccessControl  KernelAccess { get; private set; }

        public ACID(Stream stream, int offset)
        {
            stream.Seek(offset, SeekOrigin.Begin);

            BinaryReader reader = new BinaryReader(stream);

            RSA2048Signature = reader.ReadBytes(0x100);
            RSA2048Modulus   = reader.ReadBytes(0x100);

            Magic = reader.ReadAscii(0x4);
            if (Magic != "ACID")
            {
                throw new Exception("ACID Stream doesn't contain ACID section!");
            }

            //Size field used with the above signature (?).
            Unknown1 = reader.ReadInt32();

            reader.ReadInt32();

            //Bit0 must be 1 on retail, on devunit 0 is also allowed. Bit1 is unknown.
            Flags = reader.ReadInt32();

            TitleIdRangeMin = reader.ReadInt64();
            TitleIdRangeMax = reader.ReadInt64();

            int FsAccessControlOffset      = reader.ReadInt32();
            int FsAccessControlSize        = reader.ReadInt32();
            int ServiceAccessControlOffset = reader.ReadInt32();
            int ServiceAccessControlSize   = reader.ReadInt32();
            int KernelAccessControlOffset  = reader.ReadInt32();
            int KernelAccessControlSize    = reader.ReadInt32();

            FsAccess = new FsAccessControl(stream, offset + FsAccessControlOffset, FsAccessControlSize);

            ServiceAccess = new ServiceAccessControl(stream, offset + ServiceAccessControlOffset, ServiceAccessControlSize);

            KernelAccess = new KernelAccessControl(stream, offset + KernelAccessControlOffset, KernelAccessControlSize);
        }
    }
}
