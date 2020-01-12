using System;
using System.Runtime.InteropServices;
using LibHac.Fs;

namespace LibHac.FsService
{
    public class SaveDataIndexerLite : ISaveDataIndexer
    {
        private object Locker { get; } = new object();
        private ulong CurrentSaveDataId { get; set; } = 0x4000000000000000;
        private bool IsKeyValueSet { get; set; }

        private SaveDataAttribute _key;
        private SaveDataIndexerValue _value;

        public Result Commit()
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

        public Result Add(out ulong saveDataId, ref SaveDataAttribute key)
        {
            lock (Locker)
            {
                if (IsKeyValueSet && _key.Equals(key))
                {
                    saveDataId = default;
                    return ResultFs.SaveDataPathAlreadyExists.Log();
                }

                _key = key;
                IsKeyValueSet = true;

                _value = new SaveDataIndexerValue { SaveDataId = CurrentSaveDataId };
                saveDataId = CurrentSaveDataId;
                CurrentSaveDataId++;

                return Result.Success;
            }
        }

        public Result Get(out SaveDataIndexerValue value, ref SaveDataAttribute key)
        {
            lock (Locker)
            {
                if (IsKeyValueSet && _key.Equals(key))
                {
                    value = _value;
                    return Result.Success;
                }

                value = default;
                return ResultFs.TargetNotFound.Log();
            }
        }

        public Result AddSystemSaveData(ref SaveDataAttribute key)
        {
            lock (Locker)
            {
                if (IsKeyValueSet && _key.Equals(key))
                {
                    return ResultFs.SaveDataPathAlreadyExists.Log();
                }

                _key = key;
                IsKeyValueSet = true;

                _value = new SaveDataIndexerValue();

                return Result.Success;
            }
        }

        public bool IsFull()
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
            lock (Locker)
            {
                if (IsKeyValueSet && _value.SaveDataId == saveDataId)
                {
                    key = _key;
                    return Result.Success;
                }

                key = default;
                return ResultFs.TargetNotFound.Log();
            }
        }

        public Result GetBySaveDataId(out SaveDataIndexerValue value, ulong saveDataId)
        {
            lock (Locker)
            {
                if (IsKeyValueSet && _value.SaveDataId == saveDataId)
                {
                    value = _value;
                    return Result.Success;
                }

                value = default;
                return ResultFs.TargetNotFound.Log();
            }
        }

        public int GetCount()
        {
            return 1;
        }

        public Result OpenSaveDataInfoReader(out ISaveDataInfoReader infoReader)
        {
            if (IsKeyValueSet)
            {
                infoReader = new SaveDataIndexerLiteInfoReader(ref _key, ref _value);
            }
            else
            {
                infoReader = new SaveDataIndexerLiteInfoReader();
            }

            return Result.Success;
        }

        private class SaveDataIndexerLiteInfoReader : ISaveDataInfoReader
        {
            private bool _finishedIterating;
            private SaveDataInfo _info;

            public SaveDataIndexerLiteInfoReader()
            {
                _finishedIterating = true;
            }

            public SaveDataIndexerLiteInfoReader(ref SaveDataAttribute key, ref SaveDataIndexerValue value)
            {
                SaveDataIndexer.GetSaveDataInfo(out _info, ref key, ref value);
            }

            public Result ReadSaveDataInfo(out long readCount, Span<byte> saveDataInfoBuffer)
            {
                Span<SaveDataInfo> outInfo = MemoryMarshal.Cast<byte, SaveDataInfo>(saveDataInfoBuffer);

                // Nintendo doesn't check if the buffer is too small here
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
    }
}
