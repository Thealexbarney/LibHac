using System;
using System.Buffers;

namespace LibHac.IO
{
    public static class StorageExtensions
    {
        public static void CopyTo(this Storage input, Storage output, IProgressReport progress = null)
        {
            const int bufferSize = 81920;
            long remaining = Math.Min(input.Length, output.Length);
            if(remaining < 0) throw new ArgumentException("Storage must have an explicit length");
            progress?.SetTotal(remaining);

            long pos = 0;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                while (remaining > 0)
                {
                    int toCopy = (int) Math.Min(buffer.Length, remaining);
                    Span<byte> buf = buffer.AsSpan(0, toCopy);
                    input.Read(buf, pos);
                    output.Write(buf, pos);

                    remaining -= toCopy;
                    pos += toCopy;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            progress?.SetTotal(0);
        }
    }
}
