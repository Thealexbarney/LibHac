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

        public Npdm(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream);

            Magic = reader.ReadAscii(0x4);

            if (Magic != "META")
            {
                throw new Exception("NPDM Stream doesn't contain NPDM file!");
            }

            reader.ReadInt64();

            //MmuFlags, bit0: 64-bit instructions, bits1-3: address space width (1=64-bit, 2=32-bit). Needs to be <= 0xF.
            byte mmuflags = reader.ReadByte();

            Is64Bits          = (mmuflags & 1) != 0;
            AddressSpaceWidth = (mmuflags >> 1) & 7;

            reader.ReadByte();

            MainThreadPriority = reader.ReadByte(); //(0-63).
            DefaultCpuId       = reader.ReadByte();

            reader.ReadInt32();

            //System resource size (max size as of 5.x: 534773760).
            SystemResourceSize = Util.Swap32(reader.ReadInt32());

            //ProcessCategory (0: regular title, 1: kernel built-in). Should be 0 here.
            ProcessCategory = Util.Swap32(reader.ReadInt32());

            //Main entrypoint stack size.
            MainEntrypointStackSize = reader.ReadInt32();

            TitleName = reader.ReadUtf8(0x10).Trim('\0');

            ProductCode = reader.ReadBytes(0x10);

            stream.Seek(0x30, SeekOrigin.Current);

            int ACI0Offset = reader.ReadInt32();
            int ACI0Size   = reader.ReadInt32();
            int ACIDOffset = reader.ReadInt32();
            int ACIDSize   = reader.ReadInt32();

            Aci0 = new ACI0(stream, ACI0Offset);
            AciD = new ACID(stream, ACIDOffset);
        }
    }
}
