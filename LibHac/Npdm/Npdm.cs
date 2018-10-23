using System;
using System.IO;

namespace LibHac.Npdm
{
    //https://github.com/Ryujinx/Ryujinx/blob/master/Ryujinx.HLE/Loaders/Npdm/Npdm.cs
    //https://github.com/SciresM/hactool/blob/master/npdm.c
    //https://github.com/SciresM/hactool/blob/master/npdm.h
    //http://switchbrew.org/index.php?title=NPDM
    public class Npdm
    {

        public string Magic;
        public bool   Is64Bits                { get; private set; }
        public int    AddressSpaceWidth       { get; private set; }
        public byte   MainThreadPriority      { get; private set; }
        public byte   DefaultCpuId            { get; private set; }
        public int    SystemResourceSize      { get; private set; }
        public int    ProcessCategory         { get; private set; }
        public int    MainEntrypointStackSize { get; private set; }
        public string TitleName               { get; private set; }
        public byte[] ProductCode             { get; private set; }

        public ACI0 Aci0 { get; private set; }
        public ACID AciD { get; private set; }

        public Npdm(Stream Stream)
        {
            BinaryReader Reader = new BinaryReader(Stream);

            Magic = Reader.ReadAscii(0x4);

            if (Magic != "META")
            {
                throw new Exception("NPDM Stream doesn't contain NPDM file!");
            }

            Reader.ReadInt64();

            //MmuFlags, bit0: 64-bit instructions, bits1-3: address space width (1=64-bit, 2=32-bit). Needs to be <= 0xF.
            byte MmuFlags = Reader.ReadByte();

            Is64Bits          = (MmuFlags & 1) != 0;
            AddressSpaceWidth = (MmuFlags >> 1) & 7;

            Reader.ReadByte();

            MainThreadPriority = Reader.ReadByte(); //(0-63).
            DefaultCpuId       = Reader.ReadByte();

            Reader.ReadInt32();

            //System resource size (max size as of 5.x: 534773760).
            SystemResourceSize = Util.Swap32(Reader.ReadInt32());

            //ProcessCategory (0: regular title, 1: kernel built-in). Should be 0 here.
            ProcessCategory = Util.Swap32(Reader.ReadInt32());

            //Main entrypoint stack size.
            MainEntrypointStackSize = Reader.ReadInt32();

            TitleName = Reader.ReadUtf8(0x10).Trim('\0');

            ProductCode = Reader.ReadBytes(0x10);

            Stream.Seek(0x30, SeekOrigin.Current);

            int ACI0Offset = Reader.ReadInt32();
            int ACI0Size   = Reader.ReadInt32();
            int ACIDOffset = Reader.ReadInt32();
            int ACIDSize   = Reader.ReadInt32();

            Aci0 = new ACI0(Stream, ACI0Offset);
            AciD = new ACID(Stream, ACIDOffset);
        }
    }
}
