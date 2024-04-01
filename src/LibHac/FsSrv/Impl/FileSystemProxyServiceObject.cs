using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSrv.Sf;
using LibHac.Ncm;
using LibHac.Sf;

namespace LibHac.FsSrv.Impl;

public static class FileSystemProxyServiceObject
{
    public static SharedRef<IFileSystemProxy> GetFileSystemProxyServiceObject(this FileSystemServerImpl fsSrv)
    {
        return new SharedRef<IFileSystemProxy>(new FileSystemProxyImpl(fsSrv.FsSrv));
    }

    public static SharedRef<IFileSystemProxyForLoader> GetFileSystemProxyForLoaderServiceObject(
        this FileSystemServerImpl fsSrv)
    {
        return new SharedRef<IFileSystemProxyForLoader>(new FileSystemProxyImpl(fsSrv.FsSrv));
    }

    public static SharedRef<IFileSystemProxyForLoader> GetInvalidFileSystemProxyForLoaderServiceObject(
        this FileSystemServerImpl fsSrv)
    {
        return new SharedRef<IFileSystemProxyForLoader>(new InvalidFileSystemProxyImplForLoader());
    }

    public static SharedRef<IProgramRegistry> GetProgramRegistryServiceObject(this FileSystemServerImpl fsSrv)
    {
        return new SharedRef<IProgramRegistry>(new ProgramRegistryImpl(fsSrv.FsSrv));
    }

    public static SharedRef<IProgramRegistry> GetInvalidProgramRegistryServiceObject(
        this FileSystemServerImpl fsSrv)
    {
        return new SharedRef<IProgramRegistry>(new InvalidProgramRegistryImpl());
    }

    private class InvalidFileSystemProxyImplForLoader : IFileSystemProxyForLoader
    {
        public void Dispose() { }

        public Result IsArchivedProgram(out bool isArchived, ulong processId)
        {
            UnsafeHelpers.SkipParamInit(out isArchived);

            return ResultFs.PortAcceptableCountLimited.Log();
        }

        public Result OpenCodeFileSystem(ref SharedRef<IFileSystem> fileSystem, OutBuffer outVerificationData,
            ref readonly FspPath path, ContentAttributes attributes, ProgramId programId)
        {
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
            InBuffer accessControlData, long accessControlDataSize, InBuffer accessControlDescriptor,
            long accessControlDescriptorSize)
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