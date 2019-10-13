using System;
using System.Collections;
using System.IO;
using LibHac.Fs;

namespace LibHac.FsSystem.Save
{
    public class DuplexBitmap
    {
        private IStorage Data { get; }
        public BitArray Bitmap { get; }

        public DuplexBitmap(IStorage bitmapStorage, int lengthBits)
        {
            Data = bitmapStorage;
            Bitmap = new BitArray(lengthBits);
            ReadBitmap(lengthBits);
        }

        private void ReadBitmap(int lengthBits)
        {
            uint mask = unchecked((uint)(1 << 31));
            var reader = new BinaryReader(Data.AsStream());
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
