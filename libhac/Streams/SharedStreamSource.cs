using System.IO;

namespace libhac.Streams
{
    public class SharedStreamSource
    {
        private Stream BaseStream { get; }
        private object Locker { get; } = new object();

        public SharedStreamSource(Stream baseStream)
        {
            BaseStream = baseStream;
        }

        public SharedStream CreateStream()
        {
            return CreateStream(0);
        }

        public SharedStream CreateStream(long offset)
        {
            return CreateStream(offset, BaseStream.Length - offset);
        }

        public SharedStream CreateStream(long offset, long length)
        {
            return new SharedStream(this, offset, length);
        }

        public void Flush() => BaseStream.Flush();

        public int Read(long readOffset, byte[] buffer, int bufferOffset, int count)
        {
            lock (Locker)
            {
                if (BaseStream.Position != readOffset)
                {
                    BaseStream.Position = readOffset;
                }

                return BaseStream.Read(buffer, bufferOffset, count);
            }
        }

        public void Write(long writeOffset, byte[] buffer, int bufferOffset, int count)
        {
            lock (Locker)
            {
                if (BaseStream.Position != writeOffset)
                {
                    BaseStream.Position = writeOffset;
                }

                BaseStream.Write(buffer, bufferOffset, count);
            }
        }

        public bool CanRead => BaseStream.CanRead;
        public bool CanSeek => BaseStream.CanSeek;
        public bool CanWrite => BaseStream.CanWrite;
        public long Length => BaseStream.Length;
    }
}
