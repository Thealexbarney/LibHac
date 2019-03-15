using System.IO;
using System.Linq;

namespace LibHac.IO.NcaUtils
{
    public class NcaFsHeader
    {
        public short Version;
        public NcaFormatType FormatType;
        public NcaHashType HashType;
        public NcaEncryptionType EncryptionType;
        public SectionType Type;

        public IvfcHeader IvfcInfo;
        public Sha256Info Sha256Info;
        public BktrPatchInfo BktrInfo;

        public byte[] Ctr;

        public NcaFsHeader(BinaryReader reader)
        {
            long start = reader.BaseStream.Position;
            Version = reader.ReadInt16();
            FormatType = (NcaFormatType)reader.ReadByte();
            HashType = (NcaHashType)reader.ReadByte();
            EncryptionType = (NcaEncryptionType)reader.ReadByte();
            reader.BaseStream.Position += 3;

            switch (HashType)
            {
                case NcaHashType.Sha256:
                    Sha256Info = new Sha256Info(reader);
                    break;
                case NcaHashType.Ivfc:
                    IvfcInfo = new IvfcHeader(reader);
                    break;
            }

            if (EncryptionType == NcaEncryptionType.AesCtrEx)
            {
                BktrInfo = new BktrPatchInfo();

                reader.BaseStream.Position = start + 0x100;

                BktrInfo.RelocationHeader = new BktrHeader(reader);
                BktrInfo.EncryptionHeader = new BktrHeader(reader);
            }

            if (FormatType == NcaFormatType.Pfs0)
            {
                Type = SectionType.Pfs0;
            }
            else if (FormatType == NcaFormatType.Romfs)
            {
                if (EncryptionType == NcaEncryptionType.AesCtrEx)
                {
                    Type = SectionType.Bktr;
                }
                else
                {
                    Type = SectionType.Romfs;
                }
            }

            reader.BaseStream.Position = start + 0x140;
            Ctr = reader.ReadBytes(8).Reverse().ToArray();

            reader.BaseStream.Position = start + 512;
        }
    }
}
