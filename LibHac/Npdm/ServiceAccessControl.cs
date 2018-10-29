using System.Collections.Generic;
using System.IO;

namespace LibHac.Npdm
{
    public class ServiceAccessControl
    {
        public Dictionary<string, bool> Services { get; private set; } = new Dictionary<string, bool>();

        public ServiceAccessControl(Stream stream, int offset, int size)
        {
            stream.Seek(offset, SeekOrigin.Begin);

            BinaryReader reader = new BinaryReader(stream);

            int bytereaded = 0;

            while (bytereaded != size)
            {
                byte controlbyte = reader.ReadByte();

                if (controlbyte == 0)
                {
                    break;
                }

                int  Length          = ((controlbyte & 0x07)) + 1;
                bool RegisterAllowed = ((controlbyte & 0x80) != 0);

                Services.Add(reader.ReadAscii(Length), RegisterAllowed);

                bytereaded += Length + 1;
            }
        }
    }
}
