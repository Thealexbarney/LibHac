using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Os;
using LibHac.Util;

namespace LibHac.FsSrv;

/// <summary>
/// Contains global functions for SaveDataSharedFileStorage.
/// </summary>
/// <remarks>Based on FS 13.1.0 (nnSdk 13.4.0)</remarks>
public static class SaveDataSharedFileStorageGlobalMethods
{
    public static Result OpenSaveDataStorage(this FileSystemServer fsSrv,
        ref SharedRef<IStorage> outSaveDataStorage, ref SharedRef<IFileSystem> baseFileSystem,
        SaveDataSpaceId spaceId, ulong saveDataId, OpenMode mode,
        Optional<SaveDataOpenTypeSetFileStorage.OpenType> type)
    {
        return fsSrv.Globals.SaveDataSharedFileStorage.SaveDataFileStorageHolder.OpenSaveDataStorage(
            ref outSaveDataStorage, ref baseFileSystem, spaceId, saveDataId, mode, type);
    }
}

internal struct SaveDataSharedFileStorageGlobals
{
    public SdkMutexType Mutex;
    public SaveDataFileStorageHolder SaveDataFileStorageHolder;

    public void Initialize(FileSystemServer fsServer)
    {
        Mutex.Initialize();
        SaveDataFileStorageHolder = new SaveDataFileStorageHolder(fsServer);
    }
}

/// <summary>
/// Provides access to a save data file from the provided <see cref="IFileSystem"/>
/// via an <see cref="IStorage"/> interface.
/// This class keeps track of which types of save data file systems have been opened from the save data file.
/// Only one of each file system type can be opened at the same time.
/// </summary>
/// <remarks>Based on FS 13.1.0 (nnSdk 13.4.0)</remarks>
public class SaveDataOpenTypeSetFileStorage : FileStorageBasedFileSystem
{
    public enum OpenType
    {
        None,
        Normal,
        Internal
    }

    private bool _isNormalStorageOpened;
    private bool _isInternalStorageOpened;
    private bool _isInternalStorageInvalidated;
    private SaveDataSpaceId _spaceId;
    private ulong _saveDataId;
    private SdkMutexType _mutex;

    // LibHac addition
    private FileSystemServer _fsServer;
    private ref SaveDataSharedFileStorageGlobals Globals => ref _fsServer.Globals.SaveDataSharedFileStorage;

    public SaveDataOpenTypeSetFileStorage(FileSystemServer fsServer, SaveDataSpaceId spaceId, ulong saveDataId)
    {
        _fsServer = fsServer;
        _spaceId = spaceId;
        _saveDataId = saveDataId;
        _mutex = new SdkMutexType();
    }

    public Result Initialize(ref SharedRef<IFileSystem> baseFileSystem, in Path path, OpenMode mode, OpenType type)
    {
        Result rc = Initialize(ref baseFileSystem, in path, mode);
        if (rc.IsFailure()) return rc;

        return SetOpenType(type);
    }

    public Result SetOpenType(OpenType type)
    {
        Assert.SdkRequires(type == OpenType.Normal || type == OpenType.Internal);

        switch (type)
        {
            case OpenType.Normal:
                if (_isNormalStorageOpened)
                    return ResultFs.TargetLocked.Log();

                _isNormalStorageOpened = true;
                return Result.Success;

            case OpenType.Internal:
                if (_isInternalStorageOpened)
                    return ResultFs.TargetLocked.Log();

                _isInternalStorageOpened = true;
                _isInternalStorageInvalidated = false;
                return Result.Success;

            default:
                Abort.UnexpectedDefault();
                return Result.Success;
        }
    }

    public void UnsetOpenType(OpenType type)
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref Globals.Mutex);

        if (type == OpenType.Normal)
        {
            _isNormalStorageOpened = false;
        }
        else if (type == OpenType.Internal)
        {
            _isInternalStorageOpened = false;
        }

        if (!IsOpened())
        {
            Globals.SaveDataFileStorageHolder.Unregister(_spaceId, _saveDataId);
        }
    }

    public void InvalidateInternalStorage()
    {
        _isInternalStorageInvalidated = true;
    }

    public bool IsInternalStorageInvalidated()
    {
        return _isInternalStorageInvalidated;
    }

    public bool IsOpened()
    {
        return _isNormalStorageOpened || _isInternalStorageOpened;
    }

    public UniqueLockRef<SdkMutexType> GetLock()
    {
        return new UniqueLockRef<SdkMutexType>(ref _mutex);
    }
}

/// <summary>
/// Handles sharing a save data file storage between an internal save data file system
/// and a normal save data file system.
/// </summary>
/// <remarks>
/// During save data import/export a save data image is opened as an "internal file system".
/// This file system allows access to portions of a save data image via an emulated file system
/// with different portions being represented as individual files. This class allows simultaneous
/// read-only access to a save data image via a normal save data file system and an internal file system.
/// Once an internal file system is opened, it will be considered valid until the save data image is
/// written to via the normal file system, at which point any accesses via the internal file system will
/// return <see cref="ResultFs.SaveDataPorterInvalidated"/>
/// <para>Based on FS 13.1.0 (nnSdk 13.4.0)</para>
/// </remarks>
public class SaveDataSharedFileStorage : IStorage
{
    private SharedRef<SaveDataOpenTypeSetFileStorage> _baseStorage;
    private SaveDataOpenTypeSetFileStorage.OpenType _type;

    public SaveDataSharedFileStorage(ref SharedRef<SaveDataOpenTypeSetFileStorage> baseStorage,
        SaveDataOpenTypeSetFileStorage.OpenType type)
    {
        _baseStorage = SharedRef<SaveDataOpenTypeSetFileStorage>.CreateMove(ref baseStorage);
        _type = type;
    }

    public override void Dispose()
    {
        if (_baseStorage.HasValue)
            _baseStorage.Get.UnsetOpenType(_type);

        _baseStorage.Destroy();

        base.Dispose();
    }

    private Result AccessCheck(bool isWriteAccess)
    {
        if (_type == SaveDataOpenTypeSetFileStorage.OpenType.Internal)
        {
            if (_baseStorage.Get.IsInternalStorageInvalidated())
                return ResultFs.SaveDataPorterInvalidated.Log();
        }
        else if (_type == SaveDataOpenTypeSetFileStorage.OpenType.Normal && isWriteAccess)
        {
            // Any opened internal file system will be invalid after a write to the normal file system
            _baseStorage.Get.InvalidateInternalStorage();
        }

        return Result.Success;
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        using UniqueLockRef<SdkMutexType> scopedLock = _baseStorage.Get.GetLock();

        Result rc = AccessCheck(isWriteAccess: false);
        if (rc.IsFailure()) return rc;

        return _baseStorage.Get.Read(offset, destination);
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        using UniqueLockRef<SdkMutexType> scopedLock = _baseStorage.Get.GetLock();

        Result rc = AccessCheck(isWriteAccess: true);
        if (rc.IsFailure()) return rc;

        return _baseStorage.Get.Write(offset, source);
    }

    public override Result SetSize(long size)
    {
        using UniqueLockRef<SdkMutexType> scopedLock = _baseStorage.Get.GetLock();

        Result rc = AccessCheck(isWriteAccess: true);
        if (rc.IsFailure()) return rc;

        return _baseStorage.Get.SetSize(size);
    }

    public override Result GetSize(out long size)
    {
        Unsafe.SkipInit(out size);

        using UniqueLockRef<SdkMutexType> scopedLock = _baseStorage.Get.GetLock();

        Result rc = AccessCheck(isWriteAccess: false);
        if (rc.IsFailure()) return rc;

        return _baseStorage.Get.GetSize(out size);
    }

    public override Result Flush()
    {
        using UniqueLockRef<SdkMutexType> scopedLock = _baseStorage.Get.GetLock();

        Result rc = AccessCheck(isWriteAccess: true);
        if (rc.IsFailure()) return rc;

        return _baseStorage.Get.Flush();
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        using UniqueLockRef<SdkMutexType> scopedLock = _baseStorage.Get.GetLock();

        Result rc = AccessCheck(isWriteAccess: true);
        if (rc.IsFailure()) return rc;

        return _baseStorage.Get.OperateRange(outBuffer, operationId, offset, size, inBuffer);
    }
}

/// <summary>
/// Holds references to any open shared save data image files.
/// </summary>
/// <remarks>Based on FS 13.1.0 (nnSdk 13.4.0)</remarks>
public class SaveDataFileStorageHolder
{
    [NonCopyable]
    private struct Entry
    {
        private SharedRef<SaveDataOpenTypeSetFileStorage> _storage;
        private SaveDataSpaceId _spaceId;
        private ulong _saveDataId;

        public Entry(ref SharedRef<SaveDataOpenTypeSetFileStorage> storage, SaveDataSpaceId spaceId,
            ulong saveDataId)
        {
            _storage = SharedRef<SaveDataOpenTypeSetFileStorage>.CreateMove(ref storage);
            _spaceId = spaceId;
            _saveDataId = saveDataId;
        }

        public void Dispose()
        {
            _storage.Destroy();
        }

        public bool Contains(SaveDataSpaceId spaceId, ulong saveDataId)
        {
            return _spaceId == spaceId && _saveDataId == saveDataId;
        }

        public SharedRef<SaveDataOpenTypeSetFileStorage> GetStorage()
        {
            return SharedRef<SaveDataOpenTypeSetFileStorage>.CreateCopy(in _storage);
        }
    }

    private LinkedList<Entry> _entryList;

    // LibHac additions
    private FileSystemServer _fsServer;
    private ref SaveDataSharedFileStorageGlobals Globals => ref _fsServer.Globals.SaveDataSharedFileStorage;

    public SaveDataFileStorageHolder(FileSystemServer fsServer)
    {
        _fsServer = fsServer;
        _entryList = new LinkedList<Entry>();
    }

    public void Dispose()
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref Globals.Mutex);

        LinkedListNode<Entry> currentEntry = _entryList.First;

        while (currentEntry is not null)
        {
            ref Entry entry = ref currentEntry.ValueRef;
            _entryList.Remove(currentEntry);
            entry.Dispose();

            currentEntry = _entryList.First;
        }
    }

    public Result OpenSaveDataStorage(ref SharedRef<IStorage> outSaveDataStorage,
        ref SharedRef<IFileSystem> baseFileSystem, SaveDataSpaceId spaceId, ulong saveDataId, OpenMode mode,
        Optional<SaveDataOpenTypeSetFileStorage.OpenType> type)
    {
        Unsafe.SkipInit(out Array18<byte> saveImageNameBuffer);

        using var saveImageName = new Path();
        Result rc = PathFunctions.SetUpFixedPathSaveId(ref saveImageName.Ref(), saveImageNameBuffer.Items, saveDataId);
        if (rc.IsFailure()) return rc;

        // If an open type isn't specified, open the save without the shared file storage layer
        if (!type.HasValue)
        {
            using var fileStorage = new SharedRef<FileStorageBasedFileSystem>(new FileStorageBasedFileSystem());
            rc = fileStorage.Get.Initialize(ref baseFileSystem, in saveImageName, mode);
            if (rc.IsFailure()) return rc;

            outSaveDataStorage.SetByMove(ref fileStorage.Ref());
            return Result.Success;
        }

        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref Globals.Mutex);

        using SharedRef<SaveDataOpenTypeSetFileStorage> baseFileStorage = GetStorage(spaceId, saveDataId);

        if (baseFileStorage.HasValue)
        {
            rc = baseFileStorage.Get.SetOpenType(type.ValueRo);
            if (rc.IsFailure()) return rc;
        }
        else
        {
            baseFileStorage.Reset(new SaveDataOpenTypeSetFileStorage(_fsServer, spaceId, saveDataId));
            rc = baseFileStorage.Get.Initialize(ref baseFileSystem, in saveImageName, mode, type.ValueRo);
            if (rc.IsFailure()) return rc;

            using SharedRef<SaveDataOpenTypeSetFileStorage> baseFileStorageCopy =
                SharedRef<SaveDataOpenTypeSetFileStorage>.CreateCopy(in baseFileStorage);

            rc = Register(ref baseFileStorageCopy.Ref(), spaceId, saveDataId);
            if (rc.IsFailure()) return rc;
        }

        outSaveDataStorage.Reset(new SaveDataSharedFileStorage(ref baseFileStorage.Ref(), type.ValueRo));

        return Result.Success;
    }

    public Result Register(ref SharedRef<SaveDataOpenTypeSetFileStorage> storage, SaveDataSpaceId spaceId,
        ulong saveDataId)
    {
        Assert.SdkRequires(Globals.Mutex.IsLockedByCurrentThread());

        _entryList.AddLast(new Entry(ref storage, spaceId, saveDataId));

        return Result.Success;
    }

    public SharedRef<SaveDataOpenTypeSetFileStorage> GetStorage(SaveDataSpaceId spaceId, ulong saveDataId)
    {
        Assert.SdkRequires(Globals.Mutex.IsLockedByCurrentThread());

        LinkedListNode<Entry> currentEntry = _entryList.First;

        while (currentEntry is not null)
        {
            if (currentEntry.ValueRef.Contains(spaceId, saveDataId))
            {
                return currentEntry.ValueRef.GetStorage();
            }

            currentEntry = currentEntry.Next;
        }

        return new SharedRef<SaveDataOpenTypeSetFileStorage>();
    }

    public void Unregister(SaveDataSpaceId spaceId, ulong saveDataId)
    {
        Assert.SdkRequires(Globals.Mutex.IsLockedByCurrentThread());

        LinkedListNode<Entry> currentEntry = _entryList.First;

        while (currentEntry is not null)
        {
            if (currentEntry.ValueRef.Contains(spaceId, saveDataId))
            {
                ref Entry entry = ref currentEntry.ValueRef;
                _entryList.Remove(currentEntry);
                entry.Dispose();

            }

            currentEntry = currentEntry.Next;
        }
    }
}