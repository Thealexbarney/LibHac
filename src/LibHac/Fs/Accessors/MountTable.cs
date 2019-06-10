using System.Collections.Generic;

using static LibHac.Results;
using static LibHac.Fs.ResultsFs;

namespace LibHac.Fs.Accessors
{
    public class MountTable
    {
        private Dictionary<string, FileSystemAccessor> Table { get; } = new Dictionary<string, FileSystemAccessor>();

        private readonly object _locker = new object();

        public Result Mount(FileSystemAccessor fileSystem)
        {
            lock (_locker)
            {
                string mountName = fileSystem.Name;

                if (Table.ContainsKey(mountName))
                {
                    return ResultFsMountNameAlreadyExists;
                }

                Table.Add(mountName, fileSystem);

                return ResultSuccess;
            }
        }

        public Result Find(string name, out FileSystemAccessor fileSystem)
        {
            lock (_locker)
            {
                if (!Table.TryGetValue(name, out fileSystem))
                {
                    return ResultFsMountNameNotFound;
                }

                return ResultSuccess;
            }
        }

        public Result Unmount(string name)
        {
            lock (_locker)
            {
                if (!Table.Remove(name))
                {
                    return ResultFsMountNameNotFound;
                }

                return ResultSuccess;
            }
        }
    }
}
