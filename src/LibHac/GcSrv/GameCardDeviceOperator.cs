using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSrv.Storage.Sf;
using LibHac.Gc;
using LibHac.Gc.Writer;
using LibHac.Os;
using LibHac.Sf;
using static LibHac.Gc.Values;

namespace LibHac.GcSrv;

/// <summary>
/// Performs various operations on the inserted game card.
/// All available operations are listed in <see cref="GameCardOperationIdValue"/>.
/// </summary>
/// <remarks>Based on nnSdk 15.3.0 (FS 15.0.0)</remarks>
internal class GameCardDeviceOperator : IStorageDeviceOperator
{
    private SharedRef<GameCardStorageDevice> _storageDevice;

    // LibHac additions
    private readonly IGcApi _gc;

    public static uint BytesToPages(long byteCount)
    {
        return (uint)((ulong)byteCount / GcPageSize);
    }

    public GameCardDeviceOperator(ref SharedRef<GameCardStorageDevice> storageDevice, IGcApi gc)
    {
        _storageDevice = SharedRef<GameCardStorageDevice>.CreateMove(ref storageDevice);
        _gc = gc;
    }

    public void Dispose()
    {
        _storageDevice.Destroy();
    }

    public Result Operate(int operationId)
    {
        return ResultFs.NotImplemented.Log();
    }

    public Result OperateIn(InBuffer buffer, long offset, long size, int operationId)
    {
        Result result;
        var operation = (GameCardOperationIdValue)operationId;

        switch (operation)
        {
            case GameCardOperationIdValue.EraseGameCard:
            {
                using var readLock = new SharedLock<ReaderWriterLock>();
                Result res = _storageDevice.Get.AcquireReadLock(ref readLock.Ref());
                if (res.IsFailure()) return res.Miss();

                if (buffer.Size != sizeof(long))
                    return ResultFs.InvalidArgument.Log();

                result = _gc.Writer.EraseAndWriteParameter((MemorySize)size, BytesToPages(buffer.As<long>()));

                break;
            }
            default:
                return ResultFs.InvalidArgument.Log();
        }

        return _storageDevice.Get.HandleGameCardAccessResult(result).Ret();
    }

    public Result OperateOut(out long bytesWritten, OutBuffer buffer, int operationId)
    {
        Result result;
        bytesWritten = 0;
        var operation = (GameCardOperationIdValue)operationId;

        using (var readLock = new SharedLock<ReaderWriterLock>())
        {
            switch (operation)
            {
                case GameCardOperationIdValue.GetGameCardIdSet:
                {
                    Result res = _storageDevice.Get.AcquireReadLock(ref readLock.Ref());
                    if (res.IsFailure()) return res.Miss();

                    if (buffer.Size < Unsafe.SizeOf<GameCardIdSet>())
                        return ResultFs.InvalidArgument.Log();

                    result = _gc.GetGameCardIdSet(out buffer.As<GameCardIdSet>());
                    bytesWritten = Unsafe.SizeOf<GameCardIdSet>();

                    break;
                }
                case GameCardOperationIdValue.GetGameCardDeviceId:
                {
                    Result res = _storageDevice.Get.AcquireReadLock(ref readLock.Ref());
                    if (res.IsFailure()) return res.Miss();

                    if (buffer.Size < GcCardDeviceIdSize)
                        return ResultFs.InvalidArgument.Log();

                    result = _gc.GetCardDeviceId(buffer.Buffer);
                    bytesWritten = GcCardDeviceIdSize;

                    break;
                }
                case GameCardOperationIdValue.GetGameCardImageHash:
                {
                    Result res = _storageDevice.Get.AcquireReadLock(ref readLock.Ref());
                    if (res.IsFailure()) return res.Miss();

                    if (buffer.Size < GcCardImageHashSize)
                        return ResultFs.InvalidArgument.Log();

                    result = _gc.GetCardImageHash(buffer.Buffer);
                    bytesWritten = GcCardImageHashSize;

                    break;
                }
                case GameCardOperationIdValue.GetGameCardDeviceCertificate:
                {
                    Result res = _storageDevice.Get.AcquireReadLock(ref readLock.Ref());
                    if (res.IsFailure()) return res.Miss();

                    if (buffer.Size < GcDeviceCertificateSize)
                        return ResultFs.InvalidArgument.Log();

                    result = _gc.GetCardDeviceCertificate(buffer.Buffer);
                    bytesWritten = GcDeviceCertificateSize;

                    break;
                }
                case GameCardOperationIdValue.GetGameCardStatus:
                {
                    Result res = _storageDevice.Get.AcquireReadLock(ref readLock.Ref());
                    if (res.IsFailure()) return res.Miss();

                    if (buffer.Size < Unsafe.SizeOf<GameCardStatus>())
                        return ResultFs.InvalidArgument.Log();

                    result = _gc.GetCardStatus(out buffer.As<GameCardStatus>());
                    bytesWritten = Unsafe.SizeOf<GameCardStatus>();

                    break;
                }
                default:
                    return ResultFs.InvalidArgument.Log();
            }
        }

        return _storageDevice.Get.HandleGameCardAccessResult(result).Ret();
    }

    public Result OperateOut2(out long bytesWrittenBuffer1, OutBuffer buffer1, out long bytesWrittenBuffer2,
        OutBuffer buffer2, int operationId)
    {
        UnsafeHelpers.SkipParamInit(out bytesWrittenBuffer1, out bytesWrittenBuffer2);

        return ResultFs.NotImplemented.Log();
    }

    public Result OperateInOut(out long bytesWritten, OutBuffer outBuffer, InBuffer inBuffer, long offset, long size,
        int operationId)
    {
        UnsafeHelpers.SkipParamInit(out bytesWritten);

        return ResultFs.NotImplemented.Log();
    }

    public Result OperateIn2Out(out long bytesWritten, OutBuffer outBuffer, InBuffer inBuffer1, InBuffer inBuffer2,
        long offset, long size, int operationId)
    {
        Result result;
        bytesWritten = 0;
        var operation = (GameCardOperationIdValue)operationId;

        switch (operation)
        {
            case GameCardOperationIdValue.ChallengeCardExistence:
            {
                using var readLock = new SharedLock<ReaderWriterLock>();
                Result res = _storageDevice.Get.AcquireReadLock(ref readLock.Ref());
                if (res.IsFailure()) return res.Miss();

                if (outBuffer.Size < GcChallengeCardExistenceResponseSize)
                    return ResultFs.InvalidArgument.Log();

                result = _gc.ChallengeCardExistence(outBuffer.Buffer, inBuffer1.Buffer, inBuffer2.Buffer);
                bytesWritten = GcChallengeCardExistenceResponseSize;

                break;
            }
            default:
                return ResultFs.InvalidArgument.Log();
        }

        return _storageDevice.Get.HandleGameCardAccessResult(result).Ret();
    }
}