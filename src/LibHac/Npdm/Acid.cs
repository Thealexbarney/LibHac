// ReSharper disable UnusedVariable
using System;
using System.IO;
using LibHac.Common.Keys;

namespace LibHac.Npdm
{
    public class Acid
    {
        public string Magic;
        public byte[] Rsa2048Signature { get; }
        public byte[] Rsa2048Modulus { get; }
        public int Size { get; }
        public int Flags { get; }

        public long TitleIdRangeMin { get; }
        public long TitleIdRangeMax { get; }

        public FsAccessControl FsAccess { get; }
        public ServiceAccessControl ServiceAccess { get; }
        public KernelAccessControl KernelAccess { get; }

        public Validity SignatureValidity { get; }

        public Acid(Stream stream, int offset) : this(stream, offset, null) { }

        public Acid(Stream stream, int offset, KeySet keySet)
        {
            stream.Seek(offset, SeekOrigin.Begin);

            var reader = new BinaryReader(stream);

            Rsa2048Signature = reader.ReadBytes(0x100);
            Rsa2048Modulus = reader.ReadBytes(0x100);

            Magic = reader.ReadAscii(0x4);
            if (Magic != "ACID")
            {
                throw new Exception("ACID Stream doesn't contain ACID section!");
            }

            Size = reader.ReadInt32();

            if (keySet != null)
            {
                reader.BaseStream.Position = offset + 0x100;
                byte[] signatureData = reader.ReadBytes(Size);
                SignatureValidity =
                    CryptoOld.Rsa2048PssVerify(signatureData, Rsa2048Signature, keySet.AcidSigningKeyParams[0].Modulus);
            }

            reader.BaseStream.Position = offset + 0x208;
            reader.ReadInt32();

            //Bit0 must be 1 on retail, on devunit 0 is also allowed. Bit1 is unknown.
            Flags = reader.ReadInt32();

            TitleIdRangeMin = reader.ReadInt64();
            TitleIdRangeMax = reader.ReadInt64();

            int fsAccessControlOffset = reader.ReadInt32();
            int fsAccessControlSize = reader.ReadInt32();
            int serviceAccessControlOffset = reader.ReadInt32();
            int serviceAccessControlSize = reader.ReadInt32();
            int kernelAccessControlOffset = reader.ReadInt32();
            int kernelAccessControlSize = reader.ReadInt32();

            FsAccess = new FsAccessControl(stream, offset + fsAccessControlOffset);

            ServiceAccess = new ServiceAccessControl(stream, offset + serviceAccessControlOffset, serviceAccessControlSize);

            KernelAccess = new KernelAccessControl(stream, offset + kernelAccessControlOffset, kernelAccessControlSize);
        }
    }
}
