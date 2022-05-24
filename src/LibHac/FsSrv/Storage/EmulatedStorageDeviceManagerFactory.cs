using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSrv.Storage.Sf;
using LibHac.Gc;
using LibHac.GcSrv;
using LibHac.Os;
using LibHac.SdmmcSrv;
using LibHac.Sf;

namespace LibHac.FsSrv.Storage;

public class EmulatedStorageDeviceManagerFactory : IStorageDeviceManagerFactory
{
    private SdkMutexType _gameCardDeviceMutex;
    private SdkMutexType _sdCardDeviceMutex;
    private SdkMutexType _mmcDeviceMutex;

    private SharedRef<SdCardManager> _sdCardDeviceManager;
    private SharedRef<DummyGameCardManager> _dummyGameCardDeviceManager;
    private SharedRef<GameCardManager> _gameCardDeviceManager;
    private SharedRef<MmcManager> _mmcDeviceManager;

    private readonly bool _hasGameCard;

    private readonly FileSystemServer _fsServer;
    private readonly GameCardDummy _gc;

    public EmulatedStorageDeviceManagerFactory(FileSystemServer fsServer, GameCardDummy gc, bool hasGameCard)
    {
        _fsServer = fsServer;
        _gc = gc;
        _hasGameCard = hasGameCard;

        _gameCardDeviceMutex = new SdkMutexType();
        _sdCardDeviceMutex = new SdkMutexType();
    }

    public void Dispose()
    {
        _sdCardDeviceManager.Destroy();
        _dummyGameCardDeviceManager.Destroy();
        _gameCardDeviceManager.Destroy();
        _mmcDeviceManager.Destroy();
    }

    public Result Create(ref SharedRef<IStorageDeviceManager> outDeviceManager, StorageDevicePortId portId)
    {
        switch (portId)
        {
            case StorageDevicePortId.Mmc:
                EnsureMmcReady();
                outDeviceManager.SetByCopy(in _mmcDeviceManager);
                break;
            case StorageDevicePortId.SdCard:
            {
                using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _sdCardDeviceMutex);

                if (!_sdCardDeviceManager.HasValue)
                    return ResultFs.StorageDeviceNotReady.Log();

                outDeviceManager.SetByCopy(in _sdCardDeviceManager);
                break;
            }
            case StorageDevicePortId.GameCard:
            {
                using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _gameCardDeviceMutex);

                if (!_dummyGameCardDeviceManager.HasValue && !_gameCardDeviceManager.HasValue)
                    return ResultFs.StorageDeviceNotReady.Log();

                if (_hasGameCard)
                {
                    outDeviceManager.SetByCopy(in _gameCardDeviceManager);
                }
                else
                {
                    outDeviceManager.SetByCopy(in _dummyGameCardDeviceManager);
                }

                break;
            }
            default:
                return ResultFs.StorageDeviceInvalidOperation.Log();
        }

        return Result.Success;
    }

    public Result SetReady(StorageDevicePortId portId, NativeHandle handle)
    {
        switch (portId)
        {
            case StorageDevicePortId.Mmc:
                EnsureMmcReady();
                break;
            case StorageDevicePortId.SdCard:
                EnsureSdCardReady();
                break;
            case StorageDevicePortId.GameCard:
                EnsureGameCardReady();
                break;
            default:
                return ResultFs.StorageDeviceInvalidOperation.Log();
        }

        return Result.Success;
    }

    public Result UnsetReady(StorageDevicePortId portId)
    {
        return ResultFs.StorageDeviceInvalidOperation.Log();
    }

    public void AwakenAll()
    {
        EnsureMmcReady();
        _mmcDeviceManager.Get.Awaken().IgnoreResult();

        using (ScopedLock.Lock(ref _sdCardDeviceMutex))
        {
            if (_sdCardDeviceManager.HasValue)
            {
                _sdCardDeviceManager.Get.Awaken().IgnoreResult();
            }
        }

        using (ScopedLock.Lock(ref _gameCardDeviceMutex))
        {
            if (_gameCardDeviceManager.HasValue)
            {
                _gameCardDeviceManager.Get.Awaken().IgnoreResult();
            }
        }
    }

    public void PutAllToSleep()
    {
        using (ScopedLock.Lock(ref _gameCardDeviceMutex))
        {
            if (_gameCardDeviceManager.HasValue)
            {
                _gameCardDeviceManager.Get.PutToSleep().IgnoreResult();
            }
        }

        using (ScopedLock.Lock(ref _sdCardDeviceMutex))
        {
            if (_sdCardDeviceManager.HasValue)
            {
                _sdCardDeviceManager.Get.PutToSleep().IgnoreResult();
            }
        }

        EnsureMmcReady();
        _mmcDeviceManager.Get.PutToSleep().IgnoreResult();
    }

    public void ShutdownAll()
    {
        using (ScopedLock.Lock(ref _gameCardDeviceMutex))
        {
            if (_gameCardDeviceManager.HasValue)
            {
                _gameCardDeviceManager.Get.Shutdown().IgnoreResult();
            }
        }

        using (ScopedLock.Lock(ref _sdCardDeviceMutex))
        {
            if (_sdCardDeviceManager.HasValue)
            {
                _sdCardDeviceManager.Get.Shutdown().IgnoreResult();
            }
        }

        EnsureMmcReady();
        _mmcDeviceManager.Get.Shutdown().IgnoreResult();
    }

    private void EnsureMmcReady()
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mmcDeviceMutex);

        if (!_mmcDeviceManager.HasValue)
        {
            // Missing: Register device address space

            _mmcDeviceManager.Reset(new MmcManager());
        }
    }

    private void EnsureSdCardReady()
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _sdCardDeviceMutex);

        if (!_sdCardDeviceManager.HasValue)
        {
            _sdCardDeviceManager.Reset(new SdCardManager());

            // Todo: BuiltInStorageFileSystemCreator::SetSdCardPortReady
        }
    }

    private void EnsureGameCardReady()
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _gameCardDeviceMutex);

        if (!_dummyGameCardDeviceManager.HasValue && !_gameCardDeviceManager.HasValue)
        {
            if (_hasGameCard)
            {
                using SharedRef<GameCardManager> manager = GameCardManager.CreateShared(_gc, _fsServer);
                _gameCardDeviceManager.SetByMove(ref manager.Ref);
            }
            else
            {
                using SharedRef<DummyGameCardManager> manager = DummyGameCardManager.CreateShared();
                _dummyGameCardDeviceManager.SetByMove(ref manager.Ref);
            }
        }
    }
}