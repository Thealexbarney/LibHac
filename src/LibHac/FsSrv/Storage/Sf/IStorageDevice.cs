using LibHac.Common;
using IStorage = LibHac.FsSrv.Sf.IStorage;

namespace LibHac.FsSrv.Storage.Sf;

// Note: This interface doesn't actually implement IStorage. We're giving it IStorage as a base because
// StorageServiceObjectAdapter is a template that is used with either IStorage or IStorageDevice
public interface IStorageDevice : IStorage
{
    Result GetHandle(out uint handle);
    Result IsHandleValid(out bool isValid);
    Result OpenOperator(ref SharedRef<IStorageDeviceOperator> outDeviceOperator);

    // These methods come from inheriting IStorage
    //Result Read(long offset, OutBuffer destination, long size);
    //Result Write(long offset, InBuffer source, long size);
    //Result Flush();
    //Result SetSize(long size);
    //Result GetSize(out long size);
    //Result OperateRange(out QueryRangeInfo rangeInfo, int operationId, long offset, long size);
}