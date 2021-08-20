using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem
{
    public class SaveDataFileSystemHolder : ForwardingFileSystem
    {
        public SaveDataFileSystemHolder(ref SharedRef<IFileSystem> baseFileSystem) : base(ref baseFileSystem)
        {
            Assert.SdkRequires(BaseFileSystem.Get.GetType() == typeof(SaveDataFileSystemHolder) ||
                               BaseFileSystem.Get.GetType() == typeof(ApplicationTemporaryFileSystem));
        }

        public SaveDataSpaceId GetSaveDataSpaceId()
        {
            IFileSystem baseFs = BaseFileSystem.Get;

            if (baseFs.GetType() == typeof(DirectorySaveDataFileSystem))
            {
                return ((DirectorySaveDataFileSystem)baseFs).GetSaveDataSpaceId();
            }

            throw new NotImplementedException();
        }

        public ulong GetSaveDataId()
        {
            IFileSystem baseFs = BaseFileSystem.Get;

            if (baseFs.GetType() == typeof(DirectorySaveDataFileSystem))
            {
                return ((DirectorySaveDataFileSystem)baseFs).GetSaveDataId();
            }

            throw new NotImplementedException();
        }

        public Result RollbackOnlyModified()
        {
            IFileSystem baseFs = BaseFileSystem.Get;

            if (baseFs.GetType() == typeof(DirectorySaveDataFileSystem))
            {
                return ((DirectorySaveDataFileSystem)baseFs).Rollback();
            }

            throw new NotImplementedException();
        }
    }
}
