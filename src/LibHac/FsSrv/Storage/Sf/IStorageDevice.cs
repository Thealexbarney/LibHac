using LibHac.Common;
using IStorage = LibHac.FsSrv.Sf.IStorage;

namespace LibHac.FsSrv.Storage.Sf;

// Note: This interface doesn't actually implement IStorage. We're giving it IStorage as a base because
// StorageServiceObjectAdapter is a template that is used with either IStorage or IStorageDevice
/// <summary>
/// Allows reading from or writing to a storage device's storage like an <see cref="IStorage"/>, getting or validating
/// its current handle, and opening an <see cref="IStorageDeviceOperator"/> for the storage device.
/// </summary>
/// <remarks>Based on nnSdk 16.2.0 (FS 16.0.0)</remarks>
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