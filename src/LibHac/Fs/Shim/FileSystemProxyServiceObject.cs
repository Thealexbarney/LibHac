using System;
using LibHac.Common;
using LibHac.FsSrv.Sf;

namespace LibHac.Fs.Shim
{
    internal struct FileSystemProxyServiceObjectGlobals
    {
        public nint FileSystemProxyServiceObjectInitGuard;
        public ReferenceCountedDisposable<IFileSystemProxy> FileSystemProxyServiceObject;

        public nint FileSystemProxyForLoaderServiceObjectInitGuard;
        public ReferenceCountedDisposable<IFileSystemProxyForLoader> FileSystemProxyForLoaderServiceObject;

        public nint ProgramRegistryServiceObjectInitGuard;
        public ReferenceCountedDisposable<IProgramRegistry> ProgramRegistryServiceObject;

        public ReferenceCountedDisposable<IFileSystemProxy> DfcFileSystemProxyServiceObject;
    }

    public static class FileSystemProxyServiceObject
    {
        private static bool HasFileSystemServer(FileSystemClient fs)
        {
            return fs.Hos is not null;
        }

        public static ReferenceCountedDisposable<IFileSystemProxy> GetFileSystemProxyServiceObject(
            this FileSystemClient fs)
        {
            ref FileSystemProxyServiceObjectGlobals g = ref fs.Globals.FileSystemProxyServiceObject;
            using var guard = new InitializationGuard(ref g.FileSystemProxyServiceObjectInitGuard,
                fs.Globals.InitMutex);

            if (!guard.IsInitialized)
            {
                g.FileSystemProxyServiceObject = GetFileSystemProxyServiceObjectImpl(fs);
            }

            return g.FileSystemProxyServiceObject.AddReference();
        }

        private static ReferenceCountedDisposable<IFileSystemProxy> GetFileSystemProxyServiceObjectImpl(
            FileSystemClient fs)
        {
            ReferenceCountedDisposable<IFileSystemProxy> dfcServiceObject =
                fs.Globals.FileSystemProxyServiceObject.DfcFileSystemProxyServiceObject;

            if (dfcServiceObject is not null)
                return dfcServiceObject.AddReference();

            if (!HasFileSystemServer(fs))
            {
                throw new InvalidOperationException("Client was not initialized with a server object.");
            }

            Result rc = fs.Hos.Sm.GetService(out IFileSystemProxy fsProxy, "fsp-srv");

            if (rc.IsFailure())
            {
                throw new HorizonResultException(rc, "Failed to get file system proxy service object.");
            }

            fsProxy.SetCurrentProcess(fs.Hos.Os.GetCurrentProcessId().Value).IgnoreResult();
            return new ReferenceCountedDisposable<IFileSystemProxy>(fsProxy);
        }

        public static ReferenceCountedDisposable<IFileSystemProxyForLoader> GetFileSystemProxyForLoaderServiceObject(
            this FileSystemClient fs)
        {
            ref FileSystemProxyServiceObjectGlobals g = ref fs.Globals.FileSystemProxyServiceObject;
            using var guard = new InitializationGuard(ref g.FileSystemProxyForLoaderServiceObjectInitGuard,
                fs.Globals.InitMutex);

            if (!guard.IsInitialized)
            {
                g.FileSystemProxyForLoaderServiceObject = GetFileSystemProxyForLoaderServiceObjectImpl(fs);
            }

            return g.FileSystemProxyForLoaderServiceObject.AddReference();
        }

        private static ReferenceCountedDisposable<IFileSystemProxyForLoader>
            GetFileSystemProxyForLoaderServiceObjectImpl(FileSystemClient fs)
        {
            if (!HasFileSystemServer(fs))
            {
                throw new InvalidOperationException("Client was not initialized with a server object.");
            }

            Result rc = fs.Hos.Sm.GetService(out IFileSystemProxyForLoader fsProxy, "fsp-ldr");

            if (rc.IsFailure())
            {
                throw new HorizonResultException(rc, "Failed to get file system proxy service object.");
            }

            fsProxy.SetCurrentProcess(fs.Hos.Os.GetCurrentProcessId().Value).IgnoreResult();
            return new ReferenceCountedDisposable<IFileSystemProxyForLoader>(fsProxy);
        }

        public static ReferenceCountedDisposable<IProgramRegistry> GetProgramRegistryServiceObject(
            this FileSystemClient fs)
        {
            ref FileSystemProxyServiceObjectGlobals g = ref fs.Globals.FileSystemProxyServiceObject;
            using var guard = new InitializationGuard(ref g.ProgramRegistryServiceObjectInitGuard,
                fs.Globals.InitMutex);

            if (!guard.IsInitialized)
            {
                g.ProgramRegistryServiceObject = GetProgramRegistryServiceObjectImpl(fs);
            }

            return g.ProgramRegistryServiceObject.AddReference();
        }

        private static ReferenceCountedDisposable<IProgramRegistry> GetProgramRegistryServiceObjectImpl(
            FileSystemClient fs)
        {
            if (!HasFileSystemServer(fs))
            {
                throw new InvalidOperationException("Client was not initialized with a server object.");
            }

            Result rc = fs.Hos.Sm.GetService(out IProgramRegistry registry, "fsp-pr");

            if (rc.IsFailure())
            {
                throw new HorizonResultException(rc, "Failed to get registry service object.");
            }

            registry.SetCurrentProcess(fs.Hos.Os.GetCurrentProcessId().Value).IgnoreResult();
            return new ReferenceCountedDisposable<IProgramRegistry>(registry);
        }

        /// <summary>
        /// Sets a <see cref="IFileSystemProxy"/> service object to use for direct function calls
        /// instead of going over IPC. If using a DFC service object, this function should be
        /// called before calling <see cref="GetFileSystemProxyServiceObject"/>.
        /// </summary>
        /// <param name="fs">The <see cref="FileSystemClient"/> to use.</param>
        /// <param name="serviceObject">The service object this <see cref="FileSystemClient"/> will use.</param>
        public static void InitializeDfcFileSystemProxyServiceObject(this FileSystemClient fs,
            ReferenceCountedDisposable<IFileSystemProxy> serviceObject)
        {
            fs.Globals.FileSystemProxyServiceObject.DfcFileSystemProxyServiceObject = serviceObject.AddReference();
        }
    }
}
