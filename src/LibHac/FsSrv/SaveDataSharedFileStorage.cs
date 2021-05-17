using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Os;
using LibHac.Util;

namespace LibHac.FsSrv
{
    public static class SaveDataSharedFileStorageGlobalMethods
    {
        public static Result OpenSaveDataStorage(this FileSystemServer fsSrv,
            out ReferenceCountedDisposable<IStorage> saveDataStorage,
            ref ReferenceCountedDisposable<IFileSystem> baseFileSystem, SaveDataSpaceId spaceId, ulong saveDataId,
            OpenMode mode, Optional<SaveDataOpenTypeSetFileStorage.OpenType> type)
        {
            return fsSrv.Globals.SaveDataSharedFileStorage.SaveDataFileStorageHolder.OpenSaveDataStorage(
                out saveDataStorage, ref baseFileSystem, spaceId, saveDataId, mode, type);
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
            _mutex.Initialize();
        }

        public Result Initialize(ref ReferenceCountedDisposable<IFileSystem> baseFileSystem, U8Span path, OpenMode mode,
            OpenType type)
        {
            Result rc = Initialize(ref baseFileSystem, path, mode);
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
            using ScopedLock<SdkMutexType> scopedLock =
                ScopedLock.Lock(ref Globals.Mutex);

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

        public UniqueLock<SdkMutexType> GetLock()
        {
            return new UniqueLock<SdkMutexType>(ref _mutex);
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
    /// return <see cref="ResultFs.SaveDataPorterInvalidated"/>.
    /// </remarks>
    public class SaveDataSharedFileStorage : IStorage
    {
        private ReferenceCountedDisposable<SaveDataOpenTypeSetFileStorage> _baseStorage;
        private SaveDataOpenTypeSetFileStorage.OpenType _type;

        public SaveDataSharedFileStorage(ref ReferenceCountedDisposable<SaveDataOpenTypeSetFileStorage> baseStorage,
            SaveDataOpenTypeSetFileStorage.OpenType type)
        {
            _baseStorage = Shared.Move(ref baseStorage);
            _type = type;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _baseStorage?.Target.UnsetOpenType(_type);
                _baseStorage?.Dispose();
            }

            base.Dispose(disposing);
        }

        private Result AccessCheck(bool isWriteAccess)
        {
            if (_type == SaveDataOpenTypeSetFileStorage.OpenType.Internal)
            {
                if (_baseStorage.Target.IsInternalStorageInvalidated())
                    return ResultFs.SaveDataPorterInvalidated.Log();
            }
            else if (_type == SaveDataOpenTypeSetFileStorage.OpenType.Normal && isWriteAccess)
            {
                // Any opened internal file system will be invalid after a write to the normal file system
                _baseStorage.Target.InvalidateInternalStorage();
            }

            return Result.Success;
        }

        protected override Result DoRead(long offset, Span<byte> destination)
        {
            using UniqueLock<SdkMutexType> scopedLock = _baseStorage.Target.GetLock();

            Result rc = AccessCheck(isWriteAccess: false);
            if (rc.IsFailure()) return rc;

            return _baseStorage.Target.Read(offset, destination);
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source)
        {
            using UniqueLock<SdkMutexType> scopedLock = _baseStorage.Target.GetLock();

            Result rc = AccessCheck(isWriteAccess: true);
            if (rc.IsFailure()) return rc;

            return _baseStorage.Target.Write(offset, source);
        }

        protected override Result DoFlush()
        {
            using UniqueLock<SdkMutexType> scopedLock = _baseStorage.Target.GetLock();

            Result rc = AccessCheck(isWriteAccess: true);
            if (rc.IsFailure()) return rc;

            return _baseStorage.Target.Flush();
        }

        protected override Result DoSetSize(long size)
        {
            using UniqueLock<SdkMutexType> scopedLock = _baseStorage.Target.GetLock();

            Result rc = AccessCheck(isWriteAccess: true);
            if (rc.IsFailure()) return rc;

            return _baseStorage.Target.SetSize(size);
        }

        protected override Result DoGetSize(out long size)
        {
            Unsafe.SkipInit(out size);

            using UniqueLock<SdkMutexType> scopedLock = _baseStorage.Target.GetLock();

            Result rc = AccessCheck(isWriteAccess: false);
            if (rc.IsFailure()) return rc;

            return _baseStorage.Target.GetSize(out size);
        }

        protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
            ReadOnlySpan<byte> inBuffer)
        {
            using UniqueLock<SdkMutexType> scopedLock = _baseStorage.Target.GetLock();

            Result rc = AccessCheck(isWriteAccess: true);
            if (rc.IsFailure()) return rc;

            return _baseStorage.Target.OperateRange(outBuffer, operationId, offset, size, inBuffer);
        }
    }

    /// <summary>
    /// Holds references to any open shared save data image files.
    /// </summary>
    public class SaveDataFileStorageHolder
    {
        private struct Entry
        {
            private ReferenceCountedDisposable<SaveDataOpenTypeSetFileStorage> _storage;
            private SaveDataSpaceId _spaceId;
            private ulong _saveDataId;

            public Entry(ref ReferenceCountedDisposable<SaveDataOpenTypeSetFileStorage> storage,
                SaveDataSpaceId spaceId, ulong saveDataId)
            {
                _storage = Shared.Move(ref storage);
                _spaceId = spaceId;
                _saveDataId = saveDataId;
            }

            public void Dispose()
            {
                _storage?.Dispose();
            }

            public bool Contains(SaveDataSpaceId spaceId, ulong saveDataId)
            {
                return _spaceId == spaceId && _saveDataId == saveDataId;
            }

            public ReferenceCountedDisposable<SaveDataOpenTypeSetFileStorage> GetStorage()
            {
                return _storage.AddReference();
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

        public Result OpenSaveDataStorage(out ReferenceCountedDisposable<IStorage> saveDataStorage,
            ref ReferenceCountedDisposable<IFileSystem> baseFileSystem, SaveDataSpaceId spaceId, ulong saveDataId,
            OpenMode mode, Optional<SaveDataOpenTypeSetFileStorage.OpenType> type)
        {
            Result rc;
            UnsafeHelpers.SkipParamInit(out saveDataStorage);

            Span<byte> saveImageName = stackalloc byte[0x30];
            var sb = new U8StringBuilder(saveImageName);
            sb.Append((byte)'/').AppendFormat(saveDataId, 'x', 16);

            // If an open type isn't specified, open the save without the shared file storage layer
            if (!type.HasValue)
            {
                ReferenceCountedDisposable<FileStorageBasedFileSystem> fileStorage = null;
                try
                {
                    fileStorage =
                        new ReferenceCountedDisposable<FileStorageBasedFileSystem>(new FileStorageBasedFileSystem());

                    rc = fileStorage.Target.Initialize(ref baseFileSystem, new U8Span(saveImageName), mode);
                    if (rc.IsFailure()) return rc;

                    saveDataStorage = fileStorage.AddReference<IStorage>();
                    return Result.Success;
                }
                finally
                {
                    fileStorage?.Dispose();
                }
            }

            using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref Globals.Mutex);

            ReferenceCountedDisposable<SaveDataOpenTypeSetFileStorage> baseFileStorage = null;
            ReferenceCountedDisposable<SaveDataOpenTypeSetFileStorage> tempBaseFileStorage = null;
            try
            {
                baseFileStorage = GetStorage(spaceId, saveDataId);

                if (baseFileStorage is not null)
                {
                    rc = baseFileStorage.Target.SetOpenType(type.ValueRo);
                    if (rc.IsFailure()) return rc;
                }
                else
                {
                    baseFileStorage =
                        new ReferenceCountedDisposable<SaveDataOpenTypeSetFileStorage>(
                            new SaveDataOpenTypeSetFileStorage(_fsServer, spaceId, saveDataId));

                    rc = baseFileStorage.Target.Initialize(ref baseFileSystem, new U8Span(saveImageName), mode,
                        type.ValueRo);
                    if (rc.IsFailure()) return rc;

                    tempBaseFileStorage = baseFileStorage.AddReference();
                    rc = Register(ref tempBaseFileStorage, spaceId, saveDataId);
                    if (rc.IsFailure()) return rc;
                }

                saveDataStorage =
                    new ReferenceCountedDisposable<IStorage>(
                        new SaveDataSharedFileStorage(ref baseFileStorage, type.ValueRo));
            }
            finally
            {
                baseFileStorage?.Dispose();
                tempBaseFileStorage?.Dispose();
            }

            return Result.Success;
        }

        public Result Register(ref ReferenceCountedDisposable<SaveDataOpenTypeSetFileStorage> storage,
            SaveDataSpaceId spaceId, ulong saveDataId)
        {
            Assert.SdkRequires(Globals.Mutex.IsLockedByCurrentThread());

            var entry = new Entry(ref storage, spaceId, saveDataId);
            _entryList.AddLast(entry);

            return Result.Success;
        }

        public ReferenceCountedDisposable<SaveDataOpenTypeSetFileStorage> GetStorage(SaveDataSpaceId spaceId,
            ulong saveDataId)
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

            return null;
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
}
