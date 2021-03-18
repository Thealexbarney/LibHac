using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.FsCreator
{
    public class EmulatedSdCardFileSystemCreator : ISdCardProxyFileSystemCreator
    {
        private const string DefaultPath = "/sdcard";

        private EmulatedSdCard SdCard { get; }
        private IFileSystem RootFileSystem { get; }
        private string Path { get; }

        private IFileSystem SdCardFileSystem { get; set; }

        public EmulatedSdCardFileSystemCreator(EmulatedSdCard sdCard, IFileSystem rootFileSystem)
        {
            SdCard = sdCard;
            RootFileSystem = rootFileSystem;
        }

        public EmulatedSdCardFileSystemCreator(EmulatedSdCard sdCard, IFileSystem rootFileSystem, string path)
        {
            SdCard = sdCard;
            RootFileSystem = rootFileSystem;
            Path = path;
        }

        public Result Create(out IFileSystem fileSystem, bool isCaseSensitive)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem);

            if (!SdCard.IsSdCardInserted())
            {
                return ResultFs.PortSdCardNoDevice.Log();
            }

            if (SdCardFileSystem != null)
            {
                fileSystem = SdCardFileSystem;

                return Result.Success;
            }

            if (RootFileSystem == null)
            {
                return ResultFs.PreconditionViolation.Log();
            }

            string path = Path ?? DefaultPath;

            // Todo: Add ProxyFileSystem?

            Result rc = Util.CreateSubFileSystem(out IFileSystem sdFileSystem, RootFileSystem, path, true);
            if (rc.IsFailure()) return rc;

            SdCardFileSystem = sdFileSystem;
            fileSystem = sdFileSystem;

            return Result.Success;
        }

        public Result Format(bool removeFromFatFsCache)
        {
            throw new NotImplementedException();
        }

        public Result Format()
        {
            throw new NotImplementedException();
        }
    }
}
