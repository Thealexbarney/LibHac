using System;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.FsCreator
{
    public class TargetManagerFileSystemCreator : ITargetManagerFileSystemCreator
    {
        public Result Create(out IFileSystem fileSystem, bool openCaseSensitive)
        {
            throw new NotImplementedException();
        }

        public Result Create(out ReferenceCountedDisposable<IFileSystem> fileSystem, bool openCaseSensitive)
        {
            throw new NotImplementedException();
        }

        public Result NormalizeCaseOfPath(out bool isSupported, Span<byte> path)
        {
            throw new NotImplementedException();
        }
    }
}
