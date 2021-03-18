using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSrv.Sf;
using LibHac.Ncm;
using LibHac.Sf;

namespace LibHac.FsSrv.Impl
{
    public static class FileSystemProxyServiceObject
    {
        public static ReferenceCountedDisposable<IFileSystemProxy> GetFileSystemProxyServiceObject(
            this FileSystemServerImpl fsSrv)
        {
            return new ReferenceCountedDisposable<IFileSystemProxy>(new FileSystemProxyImpl(fsSrv.FsSrv));
        }

        public static ReferenceCountedDisposable<IFileSystemProxyForLoader> GetFileSystemProxyForLoaderServiceObject(
            this FileSystemServerImpl fsSrv)
        {
            return new ReferenceCountedDisposable<IFileSystemProxyForLoader>(new FileSystemProxyImpl(fsSrv.FsSrv));
        }

        public static ReferenceCountedDisposable<IFileSystemProxyForLoader>
            GetInvalidFileSystemProxyForLoaderServiceObject(this FileSystemServerImpl fsSrv)
        {
            return new ReferenceCountedDisposable<IFileSystemProxyForLoader>(new InvalidFileSystemProxyImplForLoader());
        }

        public static ReferenceCountedDisposable<IProgramRegistry> GetProgramRegistryServiceObject(
            this FileSystemServerImpl fsSrv)
        {
            return new ReferenceCountedDisposable<IProgramRegistry>(new ProgramRegistryImpl(fsSrv.FsSrv));
        }

        public static ReferenceCountedDisposable<IProgramRegistry> GetInvalidProgramRegistryServiceObject(
            this FileSystemServerImpl fsSrv)
        {
            return new ReferenceCountedDisposable<IProgramRegistry>(new InvalidProgramRegistryImpl());
        }

        private class InvalidFileSystemProxyImplForLoader : IFileSystemProxyForLoader
        {
            public void Dispose() { }

            public Result IsArchivedProgram(out bool isArchived, ulong processId)
            {
                UnsafeHelpers.SkipParamInit(out isArchived);

                return ResultFs.PortAcceptableCountLimited.Log();
            }

            public Result OpenCodeFileSystem(out ReferenceCountedDisposable<IFileSystem> fileSystem,
                out CodeVerificationData verificationData, in FspPath path, ProgramId programId)
            {
                UnsafeHelpers.SkipParamInit(out fileSystem, out verificationData);

                return ResultFs.PortAcceptableCountLimited.Log();
            }

            public Result SetCurrentProcess(ulong processId)
            {
                return ResultFs.PortAcceptableCountLimited.Log();
            }
        }

        private class InvalidProgramRegistryImpl : IProgramRegistry
        {
            public void Dispose() { }

            public Result RegisterProgram(ulong processId, ProgramId programId, StorageId storageId,
                InBuffer accessControlData, InBuffer accessControlDescriptor)
            {
                return ResultFs.PortAcceptableCountLimited.Log();
            }

            public Result SetCurrentProcess(ulong processId)
            {
                return ResultFs.PortAcceptableCountLimited.Log();
            }

            public Result UnregisterProgram(ulong processId)
            {
                return ResultFs.PortAcceptableCountLimited.Log();
            }
        }
    }
}
