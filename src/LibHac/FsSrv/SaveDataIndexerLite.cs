using System;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSrv.Sf;
using LibHac.Sf;

namespace LibHac.FsSrv
{
    /// <summary>
    /// Indexes metadata for temporary save data, holding a key-value pair of types
    /// <see cref="SaveDataAttribute"/> and <see cref="SaveDataIndexerValue"/> respectively. 
    /// </summary>
    /// <remarks>
    /// Only one temporary save data may exist at a time. When a new
    /// save data is added to the index, the existing key-value pair is replaced.
    /// <br/>Based on FS 10.0.0 (nnSdk 10.4.0)
    /// </remarks>
    public class SaveDataIndexerLite : ISaveDataIndexer
    {
        private object Locker { get; } = new object();
        private ulong CurrentSaveDataId { get; set; } = 0x4000000000000000;

        // Todo: Use Optional<T>
        private bool IsKeyValueSet { get; set; }

        private SaveDataAttribute _key;
        private SaveDataIndexerValue _value;

        public Result Commit()
        {
            return Result.Success;
        }

        public Result Rollback()
        {
            return Result.Success;
        }

        public Result Reset()
        {
            lock (Locker)
            {
                IsKeyValueSet = false;
                return Result.Success;
            }
        }

        public Result Publish(out ulong saveDataId, in SaveDataAttribute key)
        {
            UnsafeHelpers.SkipParamInit(out saveDataId);

            lock (Locker)
            {
                if (IsKeyValueSet && _key == key)
                    return ResultFs.AlreadyExists.Log();

                _key = key;
                IsKeyValueSet = true;

                _value = new SaveDataIndexerValue { SaveDataId = CurrentSaveDataId };
                saveDataId = CurrentSaveDataId;
                CurrentSaveDataId++;

                return Result.Success;
            }
        }

        public Result Get(out SaveDataIndexerValue value, in SaveDataAttribute key)
        {
            UnsafeHelpers.SkipParamInit(out value);

            lock (Locker)
            {
                if (IsKeyValueSet && _key == key)
                {
                    value = _value;
                    return Result.Success;
                }

                return ResultFs.TargetNotFound.Log();
            }
        }

        public Result PutStaticSaveDataIdIndex(in SaveDataAttribute key)
        {
            lock (Locker)
            {
                if (IsKeyValueSet && _key == key)
                    return ResultFs.AlreadyExists.Log();

                _key = key;
                IsKeyValueSet = true;

                _value = new SaveDataIndexerValue();
                return Result.Success;
            }
        }

        public bool IsRemainedReservedOnly()
        {
            return false;
        }

        public Result Delete(ulong saveDataId)
        {
            lock (Locker)
            {
                if (IsKeyValueSet && _value.SaveDataId == saveDataId)
                {
                    IsKeyValueSet = false;
                    return Result.Success;
                }

                return ResultFs.TargetNotFound.Log();
            }
        }

        public Result SetSpaceId(ulong saveDataId, SaveDataSpaceId spaceId)
        {
            lock (Locker)
            {
                if (IsKeyValueSet && _value.SaveDataId == saveDataId)
                {
                    _value.SpaceId = spaceId;
                    return Result.Success;
                }

                return ResultFs.TargetNotFound.Log();
            }
        }

        public Result SetSize(ulong saveDataId, long size)
        {
            // Nintendo doesn't lock in this function for some reason
            lock (Locker)
            {
                if (IsKeyValueSet && _value.SaveDataId == saveDataId)
                {
                    _value.Size = size;
                    return Result.Success;
                }

                return ResultFs.TargetNotFound.Log();
            }
        }

        public Result SetState(ulong saveDataId, SaveDataState state)
        {
            // Nintendo doesn't lock in this function for some reason
            lock (Locker)
            {
                if (IsKeyValueSet && _value.SaveDataId == saveDataId)
                {
                    _value.State = state;
                    return Result.Success;
                }

                return ResultFs.TargetNotFound.Log();
            }
        }

        public Result GetKey(out SaveDataAttribute key, ulong saveDataId)
        {
            UnsafeHelpers.SkipParamInit(out key);

            lock (Locker)
            {
                if (IsKeyValueSet && _value.SaveDataId == saveDataId)
                {
                    key = _key;
                    return Result.Success;
                }

                return ResultFs.TargetNotFound.Log();
            }
        }

        public Result GetValue(out SaveDataIndexerValue value, ulong saveDataId)
        {
            UnsafeHelpers.SkipParamInit(out value);

            lock (Locker)
            {
                if (IsKeyValueSet && _value.SaveDataId == saveDataId)
                {
                    value = _value;
                    return Result.Success;
                }

                return ResultFs.TargetNotFound.Log();
            }
        }

        public Result SetValue(in SaveDataAttribute key, in SaveDataIndexerValue value)
        {
            lock (Locker)
            {
                if (IsKeyValueSet && _key == key)
                {
                    _value = value;
                    return Result.Success;
                }

                return ResultFs.TargetNotFound.Log();
            }
        }

        public int GetIndexCount()
        {
            return 1;
        }

        public Result OpenSaveDataInfoReader(out ReferenceCountedDisposable<SaveDataInfoReaderImpl> infoReader)
        {
            SaveDataIndexerLiteInfoReader reader;

            if (IsKeyValueSet)
            {
                reader = new SaveDataIndexerLiteInfoReader(in _key, in _value);
            }
            else
            {
                reader = new SaveDataIndexerLiteInfoReader();
            }

            infoReader = new ReferenceCountedDisposable<SaveDataInfoReaderImpl>(reader);

            return Result.Success;
        }

        private class SaveDataIndexerLiteInfoReader : SaveDataInfoReaderImpl, ISaveDataInfoReader
        {
            private bool _finishedIterating;
            private SaveDataInfo _info;

            public SaveDataIndexerLiteInfoReader()
            {
                _finishedIterating = true;
            }

            public SaveDataIndexerLiteInfoReader(in SaveDataAttribute key, in SaveDataIndexerValue value)
            {
                SaveDataIndexer.GenerateSaveDataInfo(out _info, in key, in value);
            }

            public Result Read(out long readCount, OutBuffer saveDataInfoBuffer)
            {
                Span<SaveDataInfo> outInfo = MemoryMarshal.Cast<byte, SaveDataInfo>(saveDataInfoBuffer.Buffer);

                // Note: Nintendo doesn't check if the buffer is large enough here
                if (_finishedIterating || outInfo.IsEmpty)
                {
                    readCount = 0;
                }
                else
                {
                    outInfo[0] = _info;
                    readCount = 1;
                    _finishedIterating = true;
                }

                return Result.Success;
            }

            public void Dispose() { }
        }

        public void Dispose() { }
    }
}
