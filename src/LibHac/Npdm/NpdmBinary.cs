// ReSharper disable UnusedVariable
using System;
using System.Buffers.Binary;
using System.IO;
using LibHac.Common.Keys;

namespace LibHac.Npdm
{
    //https://github.com/Ryujinx/Ryujinx/blob/master/Ryujinx.HLE/Loaders/Npdm/Npdm.cs
    //https://github.com/SciresM/hactool/blob/master/npdm.c
    //https://github.com/SciresM/hactool/blob/master/npdm.h
    //http://switchbrew.org/index.php?title=NPDM
    public class NpdmBinary
    {
        public string Magic;
        public bool   Is64Bits                { get; }
        public int    AddressSpaceWidth       { get; }
        public byte   MainThreadPriority      { get; }
        public byte   DefaultCpuId            { get; }
        public int    SystemResourceSize      { get; }
        public int    ProcessCategory         { get; }
        public int    MainEntrypointStackSize { get; }
        public string TitleName               { get; }
        public byte[] ProductCode             { get; }

        public Aci0 Aci0 { get; }
        public Acid AciD { get; }

        public NpdmBinary(Stream stream) : this(stream, null) { }

        public NpdmBinary(Stream stream, KeySet keySet)
        {
            var reader = new BinaryReader(stream);

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
            SystemResourceSize = BinaryPrimitives.ReverseEndianness(reader.ReadInt32());

            //ProcessCategory (0: regular title, 1: kernel built-in). Should be 0 here.
            ProcessCategory = BinaryPrimitives.ReverseEndianness(reader.ReadInt32());

            //Main entrypoint stack size.
            MainEntrypointStackSize = reader.ReadInt32();

            TitleName = reader.ReadUtf8(0x10).Trim('\0');

            ProductCode = reader.ReadBytes(0x10);

            stream.Seek(0x30, SeekOrigin.Current);

            int aci0Offset = reader.ReadInt32();
            int aci0Size   = reader.ReadInt32();
            int acidOffset = reader.ReadInt32();
            int acidSize   = reader.ReadInt32();

            Aci0 = new Aci0(stream, aci0Offset);
            AciD = new Acid(stream, acidOffset, keySet);
        }
    }
}
