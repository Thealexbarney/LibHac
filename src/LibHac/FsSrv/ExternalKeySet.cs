using System;
using System.Collections.Generic;
using LibHac.Fs;
using LibHac.Spl;

namespace LibHac.FsSrv
{
    public class ExternalKeySet
    {
        private readonly object _locker = new object();

        private Dictionary<RightsId, AccessKey> ExternalKeys { get; set; } = new Dictionary<RightsId, AccessKey>();

        public Result Add(RightsId rightsId, AccessKey key)
        {
            lock (_locker)
            {
                if (ExternalKeys.TryGetValue(rightsId, out AccessKey existingKey))
                {
                    if (key == existingKey)
                    {
                        return Result.Success;
                    }

                    return ResultFs.ExternalKeyAlreadyRegistered.Log();
                }

                ExternalKeys.Add(rightsId, key);
            }

            return Result.Success;
        }

        public Result Get(RightsId rightsId, out AccessKey key)
        {
            lock (_locker)
            {
                if (ExternalKeys.TryGetValue(rightsId, out key))
                {
                    return Result.Success;
                }

                return ResultFs.NcaExternalKeyUnavailable.Log();
            }
        }

        public bool Contains(RightsId rightsId)
        {
            lock (_locker)
            {
                return ExternalKeys.ContainsKey(rightsId);
            }
        }

        public bool Remove(RightsId rightsId)
        {
            lock (_locker)
            {
                return ExternalKeys.Remove(rightsId);
            }
        }

        public void Clear()
        {
            lock (_locker)
            {
                ExternalKeys.Clear();
            }
        }

        public List<(RightsId rightsId, AccessKey key)> ToList()
        {
            lock (_locker)
            {
                var list = new List<(RightsId rightsId, AccessKey key)>(ExternalKeys.Count);

                foreach (KeyValuePair<RightsId, AccessKey> kvp in ExternalKeys)
                {
                    list.Add((kvp.Key, kvp.Value));
                }

                return list;
            }
        }

        public void TrimExcess() => TrimExcess(0);

        public void TrimExcess(int capacity)
        {
            lock (_locker)
            {
                int newCapacity = Math.Max(capacity, ExternalKeys.Count);
                ExternalKeys.TrimExcess(newCapacity);
            }
        }

        public void EnsureCapacity(int capacity)
        {
            ExternalKeys.EnsureCapacity(capacity);
        }
    }
}
