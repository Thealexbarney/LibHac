using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace LibHac
{
    class ServiceAccessControl
    {
        public Dictionary<string, bool> Services { get; private set; } = new Dictionary<string, bool>();

        public ServiceAccessControl(Stream Stream, int Offset, int Size)
        {
            Stream.Seek(Offset, SeekOrigin.Begin);

            BinaryReader Reader = new BinaryReader(Stream);

            int ByteReaded = 0;

            while (ByteReaded != Size)
            {
                byte ControlByte = Reader.ReadByte();

                if (ControlByte == 0)
                {
                    break;
                }

                int  Length          = ((ControlByte & 0x07)) + 1;
                bool RegisterAllowed = ((ControlByte & 0x80) != 0);

                Services.Add(Reader.ReadAscii(Length), RegisterAllowed);

                ByteReaded += Length + 1;
            }
        }
    }
}
