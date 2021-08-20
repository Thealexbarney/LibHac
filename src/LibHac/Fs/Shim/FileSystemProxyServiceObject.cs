using LibHac.Common;
using LibHac.FsSrv.Sf;

namespace LibHac.Fs.Shim
{
    internal struct FileSystemProxyServiceObjectGlobals
    {
        public nint FileSystemProxyServiceObjectInitGuard;
        public SharedRef<IFileSystemProxy> FileSystemProxyServiceObject;

        public nint FileSystemProxyForLoaderServiceObjectInitGuard;
        public SharedRef<IFileSystemProxyForLoader> FileSystemProxyForLoaderServiceObject;

        public nint ProgramRegistryServiceObjectInitGuard;
        public SharedRef<IProgramRegistry> ProgramRegistryServiceObject;

        public SharedRef<IFileSystemProxy> DfcFileSystemProxyServiceObject;
    }

    public static class FileSystemProxyServiceObject
    {
        public static SharedRef<IFileSystemProxy> GetFileSystemProxyServiceObject(this FileSystemClientImpl fs)
        {
            ref FileSystemProxyServiceObjectGlobals g = ref fs.Globals.FileSystemProxyServiceObject;
            using var guard = new InitializationGuard(ref g.FileSystemProxyServiceObjectInitGuard,
                fs.Globals.InitMutex);

            if (!guard.IsInitialized)
            {
                using SharedRef<IFileSystemProxy> createdObject = GetFileSystemProxyServiceObjectImpl(fs);
                g.FileSystemProxyServiceObject.SetByMove(ref createdObject.Ref());
            }

            return SharedRef<IFileSystemProxy>.CreateCopy(ref g.FileSystemProxyServiceObject);
        }

        private static SharedRef<IFileSystemProxy> GetFileSystemProxyServiceObjectImpl(FileSystemClientImpl fs)
        {
            ref SharedRef<IFileSystemProxy> dfcServiceObject =
                ref fs.Globals.FileSystemProxyServiceObject.DfcFileSystemProxyServiceObject;

            if (dfcServiceObject.HasValue)
            {
                return SharedRef<IFileSystemProxy>.CreateCopy(ref dfcServiceObject);
            }

            using var fileSystemProxy = new SharedRef<IFileSystemProxy>();
            Result rc = fs.Hos.Sm.GetService(ref fileSystemProxy.Ref(), "fsp-srv");

            if (rc.IsFailure())
            {
                throw new HorizonResultException(rc, "Failed to get file system proxy service object.");
            }

            fileSystemProxy.Get.SetCurrentProcess(fs.Hos.Os.GetCurrentProcessId().Value).IgnoreResult();
            return SharedRef<IFileSystemProxy>.CreateMove(ref fileSystemProxy.Ref());
        }

        public static SharedRef<IFileSystemProxyForLoader> GetFileSystemProxyForLoaderServiceObject(
            this FileSystemClientImpl fs)
        {
            ref FileSystemProxyServiceObjectGlobals g = ref fs.Globals.FileSystemProxyServiceObject;
            using var guard = new InitializationGuard(ref g.FileSystemProxyForLoaderServiceObjectInitGuard,
                fs.Globals.InitMutex);

            if (!guard.IsInitialized)
            {
                using SharedRef<IFileSystemProxyForLoader> createdObject = GetFileSystemProxyForLoaderServiceObjectImpl(fs);
                g.FileSystemProxyForLoaderServiceObject.SetByMove(ref createdObject.Ref());
            }

            return SharedRef<IFileSystemProxyForLoader>.CreateCopy(ref g.FileSystemProxyForLoaderServiceObject);
        }

        private static SharedRef<IFileSystemProxyForLoader> GetFileSystemProxyForLoaderServiceObjectImpl(
            FileSystemClientImpl fs)
        {
            using var fileSystemProxy = new SharedRef<IFileSystemProxyForLoader>();
            Result rc = fs.Hos.Sm.GetService(ref fileSystemProxy.Ref(), "fsp-ldr");

            if (rc.IsFailure())
            {
                throw new HorizonResultException(rc, "Failed to get file system proxy service object.");
            }

            fileSystemProxy.Get.SetCurrentProcess(fs.Hos.Os.GetCurrentProcessId().Value).IgnoreResult();
            return SharedRef<IFileSystemProxyForLoader>.CreateMove(ref fileSystemProxy.Ref());
        }

        public static SharedRef<IProgramRegistry> GetProgramRegistryServiceObject(this FileSystemClientImpl fs)
        {
            ref FileSystemProxyServiceObjectGlobals g = ref fs.Globals.FileSystemProxyServiceObject;
            using var guard = new InitializationGuard(ref g.ProgramRegistryServiceObjectInitGuard,
                fs.Globals.InitMutex);

            if (!guard.IsInitialized)
            {
                using SharedRef<IProgramRegistry> createdObject = GetProgramRegistryServiceObjectImpl(fs);
                g.ProgramRegistryServiceObject.SetByMove(ref createdObject.Ref());
            }

            return SharedRef<IProgramRegistry>.CreateCopy(ref g.ProgramRegistryServiceObject);
        }

        private static SharedRef<IProgramRegistry> GetProgramRegistryServiceObjectImpl(FileSystemClientImpl fs)
        {
            using var registry = new SharedRef<IProgramRegistry>();
            Result rc = fs.Hos.Sm.GetService(ref registry.Ref(), "fsp-pr");

            if (rc.IsFailure())
            {
                throw new HorizonResultException(rc, "Failed to get registry service object.");
            }

            registry.Get.SetCurrentProcess(fs.Hos.Os.GetCurrentProcessId().Value).IgnoreResult();
            return SharedRef<IProgramRegistry>.CreateMove(ref registry.Ref());
        }

        /// <summary>
        /// Sets an <see cref="IFileSystemProxy"/> service object to use for direct function calls
        /// instead of going over IPC. If using a DFC service object, this function should be
        /// called before calling <see cref="GetFileSystemProxyServiceObject"/>.
        /// </summary>
        /// <param name="fs">The <see cref="FileSystemClient"/> to use.</param>
        /// <param name="serviceObject">The service object this <see cref="FileSystemClient"/> will use.</param>
        public static void InitializeDfcFileSystemProxyServiceObject(this FileSystemClientImpl fs,
            ref SharedRef<IFileSystemProxy> serviceObject)
        {
            fs.Globals.FileSystemProxyServiceObject.DfcFileSystemProxyServiceObject.SetByMove(ref serviceObject);
        }
    }
}
