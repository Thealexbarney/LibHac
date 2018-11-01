﻿using System;
using LibHac.Save;

namespace LibHac.IO.Save
{
    public class HierarchicalDuplexStorage : Storage
    {
        private DuplexStorage[] Layers { get; }
        private DuplexStorage DataLayer { get; }

        public HierarchicalDuplexStorage(DuplexFsLayerInfo2[] layers, bool masterBit)
        {
            Layers = new DuplexStorage[layers.Length - 1];

            for (int i = 0; i < Layers.Length; i++)
            {
                Storage bitmap;

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
            Length = DataLayer.Length;
        }

        protected override int ReadSpan(Span<byte> destination, long offset)
        {
            return DataLayer.Read(destination, offset);
        }

        protected override void WriteSpan(ReadOnlySpan<byte> source, long offset)
        {
            throw new NotImplementedException();
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Length { get; }
    }

    public class DuplexFsLayerInfo2
    {
        public Storage DataA { get; set; }
        public Storage DataB { get; set; }
        public DuplexInfo Info { get; set; }
    }
}