using System;
using System.Collections.Generic;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.FsCreator;

public interface IBaseFileSystemCreator
{
    Result Create(ref SharedRef<IFileSystem> outFileSystem, BaseFileSystemId id);
    Result Format(BaseFileSystemId id);
}

public class BaseFileSystemCreatorHolder : IDisposable
{
    private Dictionary<BaseFileSystemId, IBaseFileSystemCreator> _creators;

    public BaseFileSystemCreatorHolder()
    {
        _creators = new Dictionary<BaseFileSystemId, IBaseFileSystemCreator>();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public Result Get(out IBaseFileSystemCreator outCreator, BaseFileSystemId id)
    {
        if (!_creators.TryGetValue(id, out outCreator))
        {
            return ResultFs.StorageDeviceInvalidOperation.Log();
        }

        return Result.Success;
    }

    public void Register(IBaseFileSystemCreator creator, BaseFileSystemId id)
    {
        _creators.TryAdd(id, creator);
    }
}