using System;

namespace LibHac.Fs.Save
{
    public class HierarchicalDuplexStorage : StorageBase
    {
        private DuplexStorage[] Layers { get; }
        private DuplexStorage DataLayer { get; }
        private long _length;

        public HierarchicalDuplexStorage(DuplexFsLayerInfo[] layers, bool masterBit)
        {
            Layers = new DuplexStorage[layers.Length - 1];

            for (int i = 0; i < Layers.Length; i++)
            {
                IStorage bitmap;

                if (i == 0)
                {
                    bitmap = masterBit ? layers[0].DataB : layers[0].DataA;
                }
                else
                {
                    bitmap = Layers[i - 1];
                }

                Layers[i] = new DuplexStorage(layers[i + 1].DataA, layers[i + 1].DataB, bitmap, layers[i + 1].Info.BlockSize);
            }

            DataLayer = Layers[Layers.Length - 1];
            DataLayer.GetSize(out _length).ThrowIfFailure();
        }

        protected override Result ReadImpl(long offset, Span<byte> destination)
        {
            return DataLayer.Read(offset, destination);
        }

        protected override Result WriteImpl(long offset, ReadOnlySpan<byte> source)
        {
            return DataLayer.Write(offset, source);
        }

        public override Result Flush()
        {
            return DataLayer.Flush();
        }

        public override Result GetSize(out long size)
        {
            size = _length;
            return Result.Success;
        }

        public void FsTrim()
        {
            foreach (DuplexStorage layer in Layers)
            {
                layer.FsTrim();
            }
        }
    }

    public class DuplexFsLayerInfo
    {
        public IStorage DataA { get; set; }
        public IStorage DataB { get; set; }
        public DuplexInfo Info { get; set; }
    }
}
