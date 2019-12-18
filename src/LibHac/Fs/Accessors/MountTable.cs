using System.Collections.Generic;

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
                    return ResultFs.MountNameAlreadyExists.Log();
                }

                Table.Add(mountName, fileSystem);

                return Result.Success;
            }
        }

        public Result Find(string name, out FileSystemAccessor fileSystem)
        {
            lock (_locker)
            {
                if (!Table.TryGetValue(name, out fileSystem))
                {
                    return ResultFs.MountNameNotFound.Log();
                }

                return Result.Success;
            }
        }

        public Result Unmount(string name)
        {
            lock (_locker)
            {
                if (!Table.TryGetValue(name, out FileSystemAccessor fsAccessor))
                {
                    return ResultFs.MountNameNotFound.Log();
                }

                Table.Remove(name);
                fsAccessor.Close();

                return Result.Success;
            }
        }
    }
}
