using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSrv.Impl;
using LibHac.Util;

namespace LibHac.FsSrv.FsCreator
{
    public class EmulatedSdCardFileSystemCreator : ISdCardProxyFileSystemCreator, IDisposable
    {
        private const string DefaultPath = "/sdcard";

        private EmulatedSdCard SdCard { get; }
        private ReferenceCountedDisposable<IFileSystem> _rootFileSystem;
        private string Path { get; }

        private ReferenceCountedDisposable<IFileSystem> _sdCardFileSystem;

        public EmulatedSdCardFileSystemCreator(EmulatedSdCard sdCard, IFileSystem rootFileSystem)
        {
            SdCard = sdCard;
            _rootFileSystem = new ReferenceCountedDisposable<IFileSystem>(rootFileSystem);
        }

        public EmulatedSdCardFileSystemCreator(EmulatedSdCard sdCard, IFileSystem rootFileSystem, string path)
        {
            SdCard = sdCard;
            _rootFileSystem = new ReferenceCountedDisposable<IFileSystem>(rootFileSystem);
            Path = path;
        }

        public void Dispose()
        {
            if (_rootFileSystem is not null)
            {
                _rootFileSystem.Dispose();
                _rootFileSystem = null;
            }

            if (_sdCardFileSystem is not null)
            {
                _sdCardFileSystem.Dispose();
                _sdCardFileSystem = null;
            }
        }

        public Result Create(out ReferenceCountedDisposable<IFileSystem> outFileSystem, bool isCaseSensitive)
        {
            UnsafeHelpers.SkipParamInit(out outFileSystem);

            if (!SdCard.IsSdCardInserted())
            {
                return ResultFs.PortSdCardNoDevice.Log();
            }

            if (_sdCardFileSystem is not null)
            {
                outFileSystem = _sdCardFileSystem.AddReference();

                return Result.Success;
            }

            if (_rootFileSystem is null)
            {
                return ResultFs.PreconditionViolation.Log();
            }

            string path = Path ?? DefaultPath;

            var sdCardPath = new Path();
            Result rc = sdCardPath.Initialize(StringUtils.StringToUtf8(path));
            if (rc.IsFailure()) return rc;

            var pathFlags = new PathFlags();
            pathFlags.AllowEmptyPath();
            rc = sdCardPath.Normalize(pathFlags);
            if (rc.IsFailure()) return rc;

            // Todo: Add ProxyFileSystem?

            ReferenceCountedDisposable<IFileSystem> tempFs = null;
            try
            {
                tempFs = _rootFileSystem.AddReference();
                rc = Utility.CreateSubDirectoryFileSystem(out _sdCardFileSystem, ref tempFs, in sdCardPath);
                if (rc.IsFailure()) return rc;

                outFileSystem = _sdCardFileSystem.AddReference();

                return Result.Success;
            }
            finally
            {
                tempFs?.Dispose();
            }
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
