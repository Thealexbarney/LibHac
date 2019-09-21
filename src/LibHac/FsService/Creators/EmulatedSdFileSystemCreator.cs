using System;
using LibHac.Fs;

namespace LibHac.FsService.Creators
{
    class EmulatedSdFileSystemCreator : ISdFileSystemCreator
    {
        private const string DefaultPath = "/sdcard";

        public IFileSystem RootFileSystem { get; set; }
        public string Path { get; set; }

        public IFileSystem SdCardFileSystem { get; set; }

        public EmulatedSdFileSystemCreator(IFileSystem rootFileSystem)
        {
            RootFileSystem = rootFileSystem;
        }

        public Result Create(out IFileSystem fileSystem)
        {
            fileSystem = default;

            if (SdCardFileSystem != null)
            {
                fileSystem = SdCardFileSystem;

                return Result.Success;
            }

            if (RootFileSystem == null)
            {
                return ResultFs.PreconditionViolation;
            }

            string path = Path ?? DefaultPath;

            return Util.CreateSubFileSystem(out fileSystem, RootFileSystem, path, true);
        }

        public Result Format(bool closeOpenEntries)
        {
            throw new NotImplementedException();
        }
    }
}
