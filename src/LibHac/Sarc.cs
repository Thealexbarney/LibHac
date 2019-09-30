using LibHac.Fs;
using LibHac.FsSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LibHac
{
    public class Sarc
    {

        private const string HeaderMagic = "SARC";

        public SarcHeader Header { get; }
     
        public SarcFilesHeader FilesHeader { get; }

        public SarcFile[] Files { get; }

        public SarcNameTable NameTable { get; }

        private IStorage Storage { get; }

        public Sarc(IStorage storage)
        {
            Storage = storage;

            using (var br = new BinaryReader(Storage.AsStream(), Encoding.Default))
            {

                Header = new SarcHeader(br);
                if (Header.Magic != HeaderMagic)
                {
                    throw new InvalidDataException("Invalid SARC file: Header magic invalid.");
                }

                FilesHeader = new SarcFilesHeader(br);

                Files = new SarcFile[FilesHeader.NodeCount];
                for (int i = 0; i <= Files.Length - 1; i++)
                {
                    Files[i] = new SarcFile(br);
                }

                NameTable = new SarcNameTable(br);

                for (int i = 0; i <= Files.Length - 1; i++)
                {

                    Files[i].FileName = br.ReadAsciiZ();
                    br.ReadBytes(Recountlenght(Files[i].FileName.Length + 1));

                    if (GetHash(Files[i].FileName, FilesHeader.HashKey) == Files[i].FileNameHash && Files[i].FileNameFlag == 1) {
                        Files[i].IsValid = Validity.Valid;
                        Files[i].Storage = new SubStorage(
                                Storage,
                                Header.DATOffset + Files[i].StartFileNode,
                                Files[i].EndFileNode - Files[i].StartFileNode
                            );
                    }
                }
            }
        }



        private uint GetHash(string name, uint multiplier)
        {
            if (name.Length == 0)
                return 0;

            uint result = 0;

            for (int x = 0; x <= name.Length - 1; x++)
            { 
                result = (result * multiplier) + Convert.ToUInt32(name[x]);
            }

            return result;
        }

        private int Recountlenght(int lenght)
        {
            var inp0 = lenght % 4;

            if (inp0 == 0)
                inp0 = 4;

            return 4 - inp0;
        }

    }

    public class SarcHeader
    {
        public string Magic;
        public ushort Lenght;
        public ushort BOM;
        public uint FileLenght;
        public uint DATOffset;
        public uint Version;
        public uint Unknown;
        public SarcHeader(BinaryReader br) {
            Magic = br.ReadAscii(4);
            Lenght = br.ReadUInt16();
            BOM = br.ReadUInt16();
            FileLenght = br.ReadUInt32();
            DATOffset = br.ReadUInt32();
            Version = br.ReadUInt16();
            Unknown = br.ReadUInt16();
        }

    }

    public class SarcFilesHeader
    {
        public char[] Magic;
        public ushort Lenght;
        public ushort NodeCount;
        public uint HashKey;

        public SarcFilesHeader(BinaryReader br)
        {
            Magic = br.ReadChars(4);
            Lenght = br.ReadUInt16();
            NodeCount = br.ReadUInt16();
            HashKey = br.ReadUInt32();
        }
    }

    public class SarcFile
    {
        public uint FileNameHash;
        public uint FileNameOffsetEntry;
        public byte FileNameFlag;
        public uint StartFileNode;
        public uint EndFileNode;


        public string FileName;
        public Validity IsValid { get; set; } = Validity.Unchecked;
        public IStorage Storage { get; set; }

        public SarcFile(BinaryReader br) {
           FileNameHash = br.ReadUInt32();
           FileNameOffsetEntry = BitConverter.ToUInt32(new byte[] { br.ReadByte(), br.ReadByte(), br.ReadByte(), 0x0 }, 0);
           FileNameFlag = br.ReadByte();
           StartFileNode = br.ReadUInt32();
           EndFileNode = br.ReadUInt32();
        }
    }

    public class SarcNameTable
    {
        public string Magic;
        public ushort Lenght;
        public ushort Unknown;

        public SarcNameTable(BinaryReader br) {
            Magic = br.ReadAscii(4);
            Lenght = br.ReadUInt16();
            Unknown = br.ReadUInt16();
        }
    }

}
