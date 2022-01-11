using System;
using LibHac.Fs;

namespace LibHac.Tools.FsSystem.Save;

public class HierarchicalDuplexStorage : IStorage
{
    private DuplexStorage[] Layers { get; }
    private DuplexStorage DataLayer { get; }
    private long Length { get; }

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
        DataLayer.GetSize(out long dataSize).ThrowIfFailure();
        Length = dataSize;
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        return DataLayer.Read(offset, destination);
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        return DataLayer.Write(offset, source);
    }

    public override Result Flush()
    {
        return DataLayer.Flush();
    }

    public override Result SetSize(long size)
    {
        return ResultFs.NotImplemented.Log();
    }

    public override Result GetSize(out long size)
    {
        size = Length;
        return Result.Success;
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        throw new NotImplementedException();
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