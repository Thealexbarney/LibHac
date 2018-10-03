// https://github.com/Ryujinx/Ryujinx/blob/0254a84f90ea03037be15b8fd1f9e0a4be5577e9/Ryujinx.HLE/Loaders/Compression/Lz4.cs

using System;

namespace LibHac
{
    public static class Lz4
    {
        public static byte[] Decompress(byte[] cmp, int decLength)
        {
            var dec = new byte[decLength];

            int cmpPos = 0;
            int decPos = 0;

            int GetLength(int length)
            {
                byte sum;

                if (length == 0xf)
                {
                    do
                    {
                        length += sum = cmp[cmpPos++];
                    }
                    while (sum == 0xff);
                }

                return length;
            }

            do
            {
                byte token = cmp[cmpPos++];

                int encCount = (token >> 0) & 0xf;
                int litCount = (token >> 4) & 0xf;

                //Copy literal chunk
                litCount = GetLength(litCount);

                Buffer.BlockCopy(cmp, cmpPos, dec, decPos, litCount);

                cmpPos += litCount;
                decPos += litCount;

                if (cmpPos >= cmp.Length)
                {
                    break;
                }

                //Copy compressed chunk
                int back = cmp[cmpPos++] << 0 |
                           cmp[cmpPos++] << 8;

                encCount = GetLength(encCount) + 4;

                int encPos = decPos - back;

                if (encCount <= back)
                {
                    Buffer.BlockCopy(dec, encPos, dec, decPos, encCount);

                    decPos += encCount;
                }
                else
                {
                    while (encCount-- > 0)
                    {
                        dec[decPos++] = dec[encPos++];
                    }
                }
            }
            while (cmpPos < cmp.Length &&
                   decPos < dec.Length);

            return dec;
        }
    }
}