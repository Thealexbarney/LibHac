using System;
using System.Text;
using LibHac.Fs;
using LibHac.FsSystem;

namespace LibHac
{

   public class Yaz0
    {
        readonly byte[] Data = { };

        public Yaz0(IStorage storage)
        {
            storage.GetSize(out long length);
            Data = new byte[(int)length];
            storage.Read(0, Data);

            //yaz in yaz compression
            while (Encoding.ASCII.GetString(Data, 0, 4) == "Yaz0" || Encoding.ASCII.GetString(Data, 0, 4) == "Yaz1")
            {
                Data = Decompress(Data);
            }
        }

        public IStorage GetStorage()
        {          
            return new MemoryStorage(Data);
        }

        private byte[] Decompress(byte[] Data)
        {
            UInt32 leng = (uint)(Data[4] << 24 | Data[5] << 16 | Data[6] << 8 | Data[7]);
            byte[] Result = new byte[leng];
            int Offs = 16;
            int dstoffs = 0;
            while (true)
            {
                byte header = Data[Offs++];
                for (int i = 0; i < 8; i++)
                {
                    if ((header & 0x80) != 0) Result[dstoffs++] = Data[Offs++];
                    else
                    {
                        byte b = Data[Offs++];
                        int offs = ((b & 0xF) << 8 | Data[Offs++]) + 1;
                        int length = (b >> 4) + 2;
                        if (length == 2) length = Data[Offs++] + 0x12;
                        for (int j = 0; j < length; j++)
                        {
                            Result[dstoffs] = Result[dstoffs - offs];
                            dstoffs++;
                        }
                    }
                    if (dstoffs >= leng) return Result;
                    header <<= 1;
                }
            }
        }

        public string GuessFileExt
        {
            get
            {
                if (Data.Length >= 8)
                {
                    string text4 = Encoding.ASCII.GetString(Data, 0, 4);
                    string text8 = Encoding.ASCII.GetString(Data, 0, 8);

                    if (text4 == "BNTX")
                        return ".bntx";
                    else if (text4 == "BNSH")
                        return ".bnsh";
                    else if (text8 == "MsgStdBn")
                        return ".msbt";
                    else if (text8 == "MsgPrjBn")
                        return ".msbp";
                    else if (text4 == "SARC")
                        return ".sarc";
                    else if (text4 == "FFNT")
                        return ".bffnt";
                    else if (text4 == "CFNT")
                        return ".bcfnt";
                    else if (text4 == "CSTM")
                        return ".bcstm";
                    else if (text4 == "FSTM")
                        return ".bfstm";
                    else if (text4 == "FSTP")
                        return ".bfstpThen";
                    else if (text4 == "CWAV")
                        return ".bcwav";
                    else if (text4 == "FWAV")
                        return ".bfwav";
                    else if (text4 == "Gfx2")
                        return "gtx";
                    else if (text4 == "FRES")
                        return ".bfres";
                    else if (text4 == "AAHS")
                        return ".sharc";
                    else if (text4 == "BAHS")
                        return ".sharcfb";
                    else if (text4 == "FSHA")
                        return ".bfsha";
                    else if (text4 == "FLAN")
                        return ".bflan";
                    else if (text4 == "FLYT")
                        return ".bflyt";
                    else if (text4 == "CLAN")
                        return ".bclan";
                    else if (text4 == "CLYT")
                        return ".bclyt";
                    else if (text4 == "CTPK")
                        return ".ctpk";
                    else if (text4 == "CGFX")
                        return ".bcres";
                    else if (text4 == "AAMP")
                        return ".aamp";
                    //else if (Encoding.Default.GetString(Data, Data.Length - 0x28, Data.Length - 0x24) == "FLIM")
                    //    return "bflim";
                    //else if (Encoding.Default.GetString(Data, Data.Length - 0x28, Data.Length - 0x24) == "CLIM")
                    //    return "bclim";
                    //else if (Encoding.Default.GetString(Data, 0, 2) == "YB" | Encoding.Default.GetString(Data, 0, 2) == "BY")
                    //    return "byml";
                    //else if (Encoding.Default.GetString(Data, 0xC, 0x10) == "SCDL")
                    //    return "bcd";

                    else
                        return ".bin";

                }
                else
                {
                    return ".bin";
                }



            }
        }


    }
}
