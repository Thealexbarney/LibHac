using System;
using LibHac.Fs;

namespace LibHac.FsService.Creators
{
    public class EmulatedSdFileSystemCreator : ISdFileSystemCreator
    {
        private const string DefaultPath = "/sdcard";

        private IFileSystem RootFileSystem { get; }
        private string Path { get; }

        private IFileSystem SdCardFileSystem { get; set; }

        public EmulatedSdFileSystemCreator(IFileSystem rootFileSystem)
        {
            RootFileSystem = rootFileSystem;
        }

        public EmulatedSdFileSystemCreator(IFileSystem rootFileSystem, string path)
        {
            RootFileSystem = rootFileSystem;
            Path = path;
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

            // Todo: Add ProxyFileSystem?

            Result rc = Util.CreateSubFileSystem(out IFileSystem sdFileSystem, RootFileSystem, path, true);
            if (rc.IsFailure()) return rc;

            SdCardFileSystem = sdFileSystem;
            fileSystem = sdFileSystem;

            return Result.Success;
        }

        public Result Format(bool closeOpenEntries)
        {
            throw new NotImplementedException();
        }
    }
}
