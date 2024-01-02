// ReSharper disable UnusedMember.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.FsSrv.Sf;

namespace LibHac.FsSrv.Impl;

public class SaveDataChunkIteratorDiff : ISaveDataChunkIterator
{
    private SaveDataChunkDiffInfo _diffInfo;
    private uint _endId;
    private bool _isExport;
    private uint _currentId;

    public SaveDataChunkIteratorDiff(in SaveDataChunkDiffInfo diffInfo, bool isExport, int count)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    private bool IsEnd()
    {
        throw new NotImplementedException();
    }

    public Result Next()
    {
        throw new NotImplementedException();
    }

    public Result IsEnd(out bool isEnd)
    {
        throw new NotImplementedException();
    }

    public Result GetId(out uint chunkId)
    {
        throw new NotImplementedException();
    }
}