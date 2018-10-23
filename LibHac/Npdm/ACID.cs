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

        public ACID(Stream Stream, int Offset)
        {
            Stream.Seek(Offset, SeekOrigin.Begin);

            BinaryReader Reader = new BinaryReader(Stream);

            RSA2048Signature = Reader.ReadBytes(0x100);
            RSA2048Modulus   = Reader.ReadBytes(0x100);

            Magic = Reader.ReadAscii(0x4);
            if (Magic != "ACID")
            {
                throw new Exception("ACID Stream doesn't contain ACID section!");
            }

            //Size field used with the above signature (?).
            Unknown1 = Reader.ReadInt32();

            Reader.ReadInt32();

            //Bit0 must be 1 on retail, on devunit 0 is also allowed. Bit1 is unknown.
            Flags = Reader.ReadInt32();

            TitleIdRangeMin = Reader.ReadInt64();
            TitleIdRangeMax = Reader.ReadInt64();

            int FsAccessControlOffset      = Reader.ReadInt32();
            int FsAccessControlSize        = Reader.ReadInt32();
            int ServiceAccessControlOffset = Reader.ReadInt32();
            int ServiceAccessControlSize   = Reader.ReadInt32();
            int KernelAccessControlOffset  = Reader.ReadInt32();
            int KernelAccessControlSize    = Reader.ReadInt32();

            FsAccess = new FsAccessControl(Stream, Offset + FsAccessControlOffset, FsAccessControlSize);

            ServiceAccess = new ServiceAccessControl(Stream, Offset + ServiceAccessControlOffset, ServiceAccessControlSize);

            KernelAccess = new KernelAccessControl(Stream, Offset + KernelAccessControlOffset, KernelAccessControlSize);
        }
    }
}
