using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSrv.Sf;
using LibHac.Util;

namespace LibHac.FsSrv.Impl
{
    internal class FileSystemInterfaceAdapter : IFileSystemSf
    {
        private ReferenceCountedDisposable<IFileSystem> BaseFileSystem { get; }
        private bool IsHostFsRoot { get; }

        // In FS, FileSystemInterfaceAdapter is derived from ISharedObject, so that's used for ref-counting when
        // creating files and directories. We don't have an ISharedObject, so a self-reference is used instead.
        private ReferenceCountedDisposable<FileSystemInterfaceAdapter>.WeakReference _selfReference;

        /// <summary>
        /// Initializes a new <see cref="FileSystemInterfaceAdapter"/> by creating
        /// a new reference to <paramref name="fileSystem"/>.
        /// </summary>
        /// <param name="fileSystem">The base file system.</param>
        /// <param name="isHostFsRoot">Does the base file system come from the root directory of a host file system?</param>
        private FileSystemInterfaceAdapter(ReferenceCountedDisposable<IFileSystem> fileSystem,
            bool isHostFsRoot = false)
        {
            BaseFileSystem = fileSystem.AddReference();
            IsHostFsRoot = isHostFsRoot;
        }

        /// <summary>
        /// Initializes a new <see cref="FileSystemInterfaceAdapter"/> by moving the file system object.
        /// Avoids allocations from incrementing and then decrementing the ref-count.
        /// </summary>
        /// <param name="fileSystem">The base file system. Will be null upon returning.</param>
        /// <param name="isHostFsRoot">Does the base file system come from the root directory of a host file system?</param>
        private FileSystemInterfaceAdapter(ref ReferenceCountedDisposable<IFileSystem> fileSystem,
            bool isHostFsRoot = false)
        {
            BaseFileSystem = fileSystem;
            fileSystem = null;
            IsHostFsRoot = isHostFsRoot;
        }

        /// <summary>
        /// Initializes a new <see cref="FileSystemInterfaceAdapter"/>, creating a copy of the input file system object.
        /// </summary>
        /// <param name="baseFileSystem">The base file system.</param>
        /// <param name="isHostFsRoot">Does the base file system come from the root directory of a host file system?</param>
        public static ReferenceCountedDisposable<FileSystemInterfaceAdapter> CreateShared(
            ReferenceCountedDisposable<IFileSystem> baseFileSystem, bool isHostFsRoot = false)
        {
            var adapter = new FileSystemInterfaceAdapter(baseFileSystem, isHostFsRoot);

            return ReferenceCountedDisposable<FileSystemInterfaceAdapter>.Create(adapter, out adapter._selfReference);
        }

        /// <summary>
        /// Initializes a new <see cref="FileSystemInterfaceAdapter"/> cast to an <see cref="IFileSystemSf"/>
        /// by moving the input file system object. Avoids allocations from incrementing and then decrementing the ref-count.
        /// </summary>
        /// <param name="baseFileSystem">The base file system. Will be null upon returning.</param>
        /// <param name="isHostFsRoot">Does the base file system come from the root directory of a host file system?</param>
        public static ReferenceCountedDisposable<IFileSystemSf> CreateSharedSfFileSystem(
            ref ReferenceCountedDisposable<IFileSystem> baseFileSystem, bool isHostFsRoot = false)
        {
            var adapter = new FileSystemInterfaceAdapter(ref baseFileSystem, isHostFsRoot);

            return ReferenceCountedDisposable<IFileSystemSf>.Create(adapter, out adapter._selfReference);
        }

        private static ReadOnlySpan<byte> RootDir => new[] { (byte)'/' };

        public Result GetImpl(out ReferenceCountedDisposable<IFileSystem> fileSystem)
        {
            fileSystem = BaseFileSystem.AddReference();
            return Result.Success;
        }

        public Result CreateFile(in Path path, long size, int option)
        {
            if (size < 0)
                return ResultFs.InvalidSize.Log();

            var normalizer = new PathNormalizer(new U8Span(path.Str), GetPathNormalizerOption());
            if (normalizer.Result.IsFailure()) return normalizer.Result;

            return BaseFileSystem.Target.CreateFile(normalizer.Path, size, (CreateFileOptions)option);
        }

        public Result DeleteFile(in Path path)
        {
            var normalizer = new PathNormalizer(new U8Span(path.Str), GetPathNormalizerOption());
            if (normalizer.Result.IsFailure()) return normalizer.Result;

            return BaseFileSystem.Target.DeleteFile(normalizer.Path);
        }

        public Result CreateDirectory(in Path path)
        {
            var normalizer = new PathNormalizer(new U8Span(path.Str), GetPathNormalizerOption());
            if (normalizer.Result.IsFailure()) return normalizer.Result;

            if (StringUtils.Compare(RootDir, normalizer.Path) != 0)
                return ResultFs.PathAlreadyExists.Log();

            return BaseFileSystem.Target.CreateDirectory(normalizer.Path);
        }

        public Result DeleteDirectory(in Path path)
        {
            var normalizer = new PathNormalizer(new U8Span(path.Str), GetPathNormalizerOption());
            if (normalizer.Result.IsFailure()) return normalizer.Result;

            if (StringUtils.Compare(RootDir, normalizer.Path) != 0)
                return ResultFs.DirectoryNotDeletable.Log();

            return BaseFileSystem.Target.DeleteDirectory(normalizer.Path);
        }

        public Result DeleteDirectoryRecursively(in Path path)
        {
            var normalizer = new PathNormalizer(new U8Span(path.Str), GetPathNormalizerOption());
            if (normalizer.Result.IsFailure()) return normalizer.Result;

            if (StringUtils.Compare(RootDir, normalizer.Path) != 0)
                return ResultFs.DirectoryNotDeletable.Log();

            return BaseFileSystem.Target.DeleteDirectoryRecursively(normalizer.Path);
        }

        public Result RenameFile(in Path oldPath, in Path newPath)
        {
            var normalizerOldPath = new PathNormalizer(new U8Span(oldPath.Str), GetPathNormalizerOption());
            if (normalizerOldPath.Result.IsFailure()) return normalizerOldPath.Result;

            var normalizerNewPath = new PathNormalizer(new U8Span(newPath.Str), GetPathNormalizerOption());
            if (normalizerNewPath.Result.IsFailure()) return normalizerNewPath.Result;

            return BaseFileSystem.Target.RenameFile(new U8Span(normalizerOldPath.Path),
                new U8Span(normalizerNewPath.Path));
        }

        public Result RenameDirectory(in Path oldPath, in Path newPath)
        {
            var normalizerOldPath = new PathNormalizer(new U8Span(oldPath.Str), GetPathNormalizerOption());
            if (normalizerOldPath.Result.IsFailure()) return normalizerOldPath.Result;

            var normalizerNewPath = new PathNormalizer(new U8Span(newPath.Str), GetPathNormalizerOption());
            if (normalizerNewPath.Result.IsFailure()) return normalizerNewPath.Result;

            if (PathTool.IsSubpath(normalizerOldPath.Path, normalizerNewPath.Path))
                return ResultFs.DirectoryNotRenamable.Log();

            return BaseFileSystem.Target.RenameDirectory(normalizerOldPath.Path, normalizerNewPath.Path);
        }

        public Result GetEntryType(out uint entryType, in Path path)
        {
            entryType = default;

            var normalizer = new PathNormalizer(new U8Span(path.Str), GetPathNormalizerOption());
            if (normalizer.Result.IsFailure()) return normalizer.Result;

            ref DirectoryEntryType type = ref Unsafe.As<uint, DirectoryEntryType>(ref entryType);

            return BaseFileSystem.Target.GetEntryType(out type, new U8Span(normalizer.Path));
        }

        public Result OpenFile(out ReferenceCountedDisposable<IFileSf> file, in Path path, uint mode)
        {
            const int maxTryCount = 2;
            file = default;

            var normalizer = new PathNormalizer(new U8Span(path.Str), GetPathNormalizerOption());
            if (normalizer.Result.IsFailure()) return normalizer.Result;

            Result rc = Result.Success;
            IFile fileInterface = null;

            for (int tryNum = 0; tryNum < maxTryCount; tryNum++)
            {
                rc = BaseFileSystem.Target.OpenFile(out fileInterface, new U8Span(normalizer.Path), (OpenMode)mode);

                // Retry on ResultDataCorrupted
                if (!ResultFs.DataCorrupted.Includes(rc))
                    break;
            }

            if (rc.IsFailure()) return rc;

            ReferenceCountedDisposable<FileSystemInterfaceAdapter> selfReference = _selfReference.TryAddReference();
            var adapter = new FileInterfaceAdapter(fileInterface, ref selfReference);
            file = new ReferenceCountedDisposable<IFileSf>(adapter);

            return Result.Success;
        }

        public Result OpenDirectory(out ReferenceCountedDisposable<IDirectorySf> directory, in Path path, uint mode)
        {
            const int maxTryCount = 2;
            directory = default;

            var normalizer = new PathNormalizer(new U8Span(path.Str), GetPathNormalizerOption());
            if (normalizer.Result.IsFailure()) return normalizer.Result;

            Result rc = Result.Success;
            IDirectory dirInterface = null;

            for (int tryNum = 0; tryNum < maxTryCount; tryNum++)
            {
                rc = BaseFileSystem.Target.OpenDirectory(out dirInterface, new U8Span(normalizer.Path), (OpenDirectoryMode)mode);

                // Retry on ResultDataCorrupted
                if (!ResultFs.DataCorrupted.Includes(rc))
                    break;
            }

            if (rc.IsFailure()) return rc;

            ReferenceCountedDisposable<FileSystemInterfaceAdapter> selfReference = _selfReference.TryAddReference();
            var adapter = new DirectoryInterfaceAdapter(dirInterface, ref selfReference);
            directory = new ReferenceCountedDisposable<IDirectorySf>(adapter);

            return Result.Success;
        }

        public Result Commit()
        {
            return BaseFileSystem.Target.Commit();
        }

        public Result GetFreeSpaceSize(out long freeSpace, in Path path)
        {
            freeSpace = default;

            var normalizer = new PathNormalizer(new U8Span(path.Str), GetPathNormalizerOption());
            if (normalizer.Result.IsFailure()) return normalizer.Result;

            return BaseFileSystem.Target.GetFreeSpaceSize(out freeSpace, normalizer.Path);
        }

        public Result GetTotalSpaceSize(out long totalSpace, in Path path)
        {
            totalSpace = default;

            var normalizer = new PathNormalizer(new U8Span(path.Str), GetPathNormalizerOption());
            if (normalizer.Result.IsFailure()) return normalizer.Result;

            return BaseFileSystem.Target.GetTotalSpaceSize(out totalSpace, normalizer.Path);
        }

        public Result CleanDirectoryRecursively(in Path path)
        {
            var normalizer = new PathNormalizer(new U8Span(path.Str), GetPathNormalizerOption());
            if (normalizer.Result.IsFailure()) return normalizer.Result;

            return BaseFileSystem.Target.CleanDirectoryRecursively(normalizer.Path);
        }

        public Result GetFileTimeStampRaw(out FileTimeStampRaw timeStamp, in Path path)
        {
            timeStamp = default;

            var normalizer = new PathNormalizer(new U8Span(path.Str), GetPathNormalizerOption());
            if (normalizer.Result.IsFailure()) return normalizer.Result;

            return BaseFileSystem.Target.GetFileTimeStampRaw(out timeStamp, normalizer.Path);
        }

        public Result QueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, int queryId, in Path path)
        {
            return BaseFileSystem.Target.QueryEntry(outBuffer, inBuffer, (QueryId)queryId, new U8Span(path.Str));
        }

        public void Dispose()
        {
            BaseFileSystem?.Dispose();
        }

        private PathNormalizer.Option GetPathNormalizerOption()
        {
            return IsHostFsRoot ? PathNormalizer.Option.PreserveUnc : PathNormalizer.Option.None;
        }
    }
}
