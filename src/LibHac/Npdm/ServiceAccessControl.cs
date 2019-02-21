using System;
using System.Collections.Generic;
using System.IO;

namespace LibHac.Npdm
{
    public class ServiceAccessControl
    {
        public List<Tuple<string, bool>> Services { get; } = new List<Tuple<string, bool>>();

        public ServiceAccessControl(Stream stream, int offset, int size)
        {
            stream.Seek(offset, SeekOrigin.Begin);

            var reader = new BinaryReader(stream);

            int bytesRead = 0;

            while (bytesRead != size)
            {
                byte controlByte = reader.ReadByte();

                if (controlByte == 0)
                {
                    break;
                }

                int  length          = ((controlByte & 0x07)) + 1;
                bool registerAllowed = ((controlByte & 0x80) != 0);

                Services.Add(new Tuple<string, bool>(reader.ReadAscii(length), registerAllowed));

                bytesRead += length + 1;
            }
        }
    }
}
