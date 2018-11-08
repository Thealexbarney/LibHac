using System;
using System.Collections;
using System.IO;

namespace LibHac.IO.Save
{
    public class DuplexBitmap
    {
        private Stream Data { get; }
        public BitArray Bitmap { get; }

        public DuplexBitmap(Storage bitmapStorage, int lengthBits)
        {
            Stream bitmapStream = bitmapStorage.AsStream();

            Data = bitmapStream;
            Bitmap = new BitArray(lengthBits);
            ReadBitmap(lengthBits);
        }

        private void ReadBitmap(int lengthBits)
        {
            uint mask = unchecked((uint)(1 << 31));
            var reader = new BinaryReader(Data);
            int bitsRemaining = lengthBits;
            int bitmapPos = 0;

            while (bitsRemaining > 0)
            {
                int bitsToRead = Math.Min(bitsRemaining, 32);
                uint val = reader.ReadUInt32();

                for (int i = 0; i < bitsToRead; i++)
                {
                    Bitmap[bitmapPos] = (val & mask) != 0;
                    bitmapPos++;
                    bitsRemaining--;
                    val <<= 1;
                }
            }
        }
    }
}
