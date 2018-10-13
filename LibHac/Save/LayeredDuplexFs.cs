using System.IO;

namespace LibHac.Save
{
    public class LayeredDuplexFs : Stream
    {
        private DuplexFs[] Layers { get; }
        private DuplexFs DataLayer { get; }

        public LayeredDuplexFs(DuplexFsLayerInfo[] layers, bool masterBit)
        {
            Layers = new DuplexFs[layers.Length - 1];

            for (int i = 0; i < Layers.Length; i++)
            {
                Stream bitmap;

                if (i == 0)
                {
                    bitmap = masterBit ? layers[0].DataB : layers[0].DataA;
                }
                else
                {
                    bitmap = Layers[i - 1];
                }

                Layers[i] = new DuplexFs(bitmap, layers[i + 1].DataA, layers[i + 1].DataB, layers[i + 1].Info.BlockSize);
            }

            DataLayer = Layers[Layers.Length - 1];
        }

        public override void Flush()
        {
            throw new System.NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return DataLayer.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return DataLayer.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            throw new System.NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new System.NotImplementedException();
        }

        public override bool CanRead => DataLayer.CanRead;
        public override bool CanSeek => DataLayer.CanSeek;
        public override bool CanWrite => DataLayer.CanWrite;
        public override long Length => DataLayer.Length;
        public override long Position
        {
            get => DataLayer.Position;
            set => DataLayer.Position = value;
        }
    }

    public class DuplexFsLayerInfo
    {
        public Stream DataA { get; set; }
        public Stream DataB { get; set; }
        public DuplexInfo Info { get; set; }
    }
}
