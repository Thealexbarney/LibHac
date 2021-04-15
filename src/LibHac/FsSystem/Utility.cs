using System;
using System.Runtime.CompilerServices;
using System.Threading;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Util;

namespace LibHac.FsSystem
{
    internal static class Utility
    {
        public delegate Result FsIterationTask(U8Span path, ref DirectoryEntry entry);

        private static U8Span RootPath => new U8Span(new[] { (byte)'/' });
        private static U8Span DirectorySeparator => RootPath;

        public static Result IterateDirectoryRecursively(IFileSystem fs, U8Span rootPath, Span<byte> workPath,
            ref DirectoryEntry dirEntry, FsIterationTask onEnterDir, FsIterationTask onExitDir, FsIterationTask onFile)
        {
            Abort.DoAbortUnless(workPath.Length >= PathTool.EntryNameLengthMax + 1);

            // Get size of the root path.
            int rootPathLen = StringUtils.GetLength(rootPath, PathTool.EntryNameLengthMax + 1);
            if (rootPathLen > PathTool.EntryNameLengthMax)
                return ResultFs.TooLongPath.Log();

            // Copy root path in, add a / if necessary.
            rootPath.Value.Slice(0, rootPathLen).CopyTo(workPath);
            if (workPath[rootPathLen - 1] != StringTraits.DirectorySeparator)
            {
                workPath[rootPathLen++] = StringTraits.DirectorySeparator;
            }

            // Make sure the result path is still valid.
            if (rootPathLen > PathTool.EntryNameLengthMax)
                return ResultFs.TooLongPath.Log();

            workPath[rootPathLen] = StringTraits.NullTerminator;

            return IterateDirectoryRecursivelyImpl(fs, workPath, ref dirEntry, onEnterDir, onExitDir, onFile);
        }

        public static Result IterateDirectoryRecursively(IFileSystem fs, U8Span rootPath, FsIterationTask onEnterDir,
            FsIterationTask onExitDir, FsIterationTask onFile)
        {
            var entry = new DirectoryEntry();
            Span<byte> workPath = stackalloc byte[PathTools.MaxPathLength + 1];

            return IterateDirectoryRecursively(fs, rootPath, workPath, ref entry, onEnterDir, onExitDir,
                onFile);
        }

        public static Result IterateDirectoryRecursively(IFileSystem fs, FsIterationTask onEnterDir,
            FsIterationTask onExitDir, FsIterationTask onFile)
        {
            return IterateDirectoryRecursively(fs, RootPath, onEnterDir, onExitDir, onFile);
        }

        private static Result IterateDirectoryRecursivelyImpl(IFileSystem fs, Span<byte> workPath,
            ref DirectoryEntry dirEntry, FsIterationTask onEnterDir, FsIterationTask onExitDir, FsIterationTask onFile)
        {
            Result rc = fs.OpenDirectory(out IDirectory dir, new U8Span(workPath), OpenDirectoryMode.All);
            if (rc.IsFailure()) return rc;

            int parentLen = StringUtils.GetLength(workPath);

            // Read and handle entries.
            while (true)
            {
                // Read a single entry.
                rc = dir.Read(out long readCount, SpanHelpers.AsSpan(ref dirEntry));
                if (rc.IsFailure()) return rc;

                // If we're out of entries, we're done.
                if (readCount == 0)
                    break;

                // Validate child path size.
                int childNameLen = StringUtils.GetLength(dirEntry.Name);
                bool isDir = dirEntry.Type == DirectoryEntryType.Directory;
                int separatorSize = isDir ? 1 : 0;

                if (parentLen + childNameLen + separatorSize >= workPath.Length)
                    return ResultFs.TooLongPath.Log();

                // Set child path.
                StringUtils.Concat(workPath, dirEntry.Name);
                {
                    if (isDir)
                    {
                        // Enter directory.
                        rc = onEnterDir(new U8Span(workPath), ref dirEntry);
                        if (rc.IsFailure()) return rc;

                        // Append separator, recurse.
                        StringUtils.Concat(workPath, DirectorySeparator);

                        rc = IterateDirectoryRecursivelyImpl(fs, workPath, ref dirEntry, onEnterDir, onExitDir, onFile);
                        if (rc.IsFailure()) return rc;

                        // Exit directory.
                        rc = onExitDir(new U8Span(workPath), ref dirEntry);
                        if (rc.IsFailure()) return rc;
                    }
                    else
                    {
                        // Call file handler.
                        rc = onFile(new U8Span(workPath), ref dirEntry);
                        if (rc.IsFailure()) return rc;
                    }
                }

                // Restore parent path.
                workPath[parentLen] = StringTraits.NullTerminator;
            }

            return Result.Success;
        }

        public static Result CopyDirectoryRecursively(IFileSystem fileSystem, U8Span destPath, U8Span sourcePath,
            Span<byte> workBuffer)
        {
            return CopyDirectoryRecursively(fileSystem, fileSystem, destPath, sourcePath, workBuffer);
        }

        public static unsafe Result CopyDirectoryRecursively(IFileSystem destFileSystem, IFileSystem sourceFileSystem,
            U8Span destPath, U8Span sourcePath, Span<byte> workBuffer)
        {
            var destPathBuf = new FsPath();
            int originalSize = StringUtils.Copy(destPathBuf.Str, destPath);
            Abort.DoAbortUnless(originalSize < Unsafe.SizeOf<FsPath>());

            // Pin and recreate the span because C# can't use byref-like types in a closure
            int workBufferSize = workBuffer.Length;
            fixed (byte* pWorkBuffer = workBuffer)
            {
                // Copy the pointer to workaround CS1764.
                // IterateDirectoryRecursively won't store the delegate anywhere, so it should be safe
                byte* pWorkBuffer2 = pWorkBuffer;

                Result OnEnterDir(U8Span path, ref DirectoryEntry entry)
                {
                    // Update path, create new dir.
                    StringUtils.Concat(SpanHelpers.AsByteSpan(ref destPathBuf), entry.Name);
                    StringUtils.Concat(SpanHelpers.AsByteSpan(ref destPathBuf), DirectorySeparator);

                    return destFileSystem.CreateDirectory(destPathBuf);
                }

                Result OnExitDir(U8Span path, ref DirectoryEntry entry)
                {
                    // Check we have a parent directory.
                    int len = StringUtils.GetLength(SpanHelpers.AsByteSpan(ref destPathBuf));
                    if (len < 2)
                        return ResultFs.InvalidPathFormat.Log();

                    // Find previous separator, add null terminator
                    int cur = len - 2;
                    while (SpanHelpers.AsByteSpan(ref destPathBuf)[cur] != StringTraits.DirectorySeparator && cur > 0)
                    {
                        cur--;
                    }

                    SpanHelpers.AsByteSpan(ref destPathBuf)[cur + 1] = StringTraits.NullTerminator;

                    return Result.Success;
                }

                Result OnFile(U8Span path, ref DirectoryEntry entry)
                {
                    var buffer = new Span<byte>(pWorkBuffer2, workBufferSize);

                    return CopyFile(destFileSystem, sourceFileSystem, destPathBuf, path, ref entry, buffer);
                }

                return IterateDirectoryRecursively(sourceFileSystem, sourcePath, OnEnterDir, OnExitDir, OnFile);
            }
        }

        public static Result CopyFile(IFileSystem destFileSystem, IFileSystem sourceFileSystem, U8Span destParentPath,
            U8Span sourcePath, ref DirectoryEntry entry, Span<byte> workBuffer)
        {
            // Open source file.
            Result rc = sourceFileSystem.OpenFile(out IFile sourceFile, sourcePath, OpenMode.Read);
            if (rc.IsFailure()) return rc;

            using (sourceFile)
            {
                // Open dest file.
                Unsafe.SkipInit(out FsPath destPath);

                var sb = new U8StringBuilder(destPath.Str);
                sb.Append(destParentPath).Append(entry.Name);

                Assert.SdkLess(sb.Length, Unsafe.SizeOf<FsPath>());

                rc = destFileSystem.CreateFile(new U8Span(destPath.Str), entry.Size);
                if (rc.IsFailure()) return rc;

                rc = destFileSystem.OpenFile(out IFile destFile, new U8Span(destPath.Str), OpenMode.Write);
                if (rc.IsFailure()) return rc;

                using (destFile)
                {
                    // Read/Write file in work buffer sized chunks.
                    long remaining = entry.Size;
                    long offset = 0;

                    while (remaining > 0)
                    {
                        rc = sourceFile.Read(out long bytesRead, offset, workBuffer, ReadOption.None);
                        if (rc.IsFailure()) return rc;

                        rc = destFile.Write(offset, workBuffer.Slice(0, (int)bytesRead), WriteOption.None);
                        if (rc.IsFailure()) return rc;

                        remaining -= bytesRead;
                        offset += bytesRead;
                    }
                }
            }

            return Result.Success;
        }

        public static Result TryAcquireCountSemaphore(out UniqueLockSemaphore uniqueLock, SemaphoreAdaptor semaphore)
        {
            UniqueLockSemaphore tempUniqueLock = default;
            try
            {
                tempUniqueLock = new UniqueLockSemaphore(semaphore);

                if (!tempUniqueLock.TryLock())
                {
                    UnsafeHelpers.SkipParamInit(out uniqueLock);
                    return ResultFs.OpenCountLimit.Log();
                }

                uniqueLock = new UniqueLockSemaphore(ref tempUniqueLock);
                return Result.Success;
            }
            finally
            {
                tempUniqueLock.Dispose();
            }
        }

        public static Result MakeUniqueLockWithPin<T>(out IUniqueLock uniqueLock, SemaphoreAdaptor semaphore,
            ref ReferenceCountedDisposable<T> objectToPin) where T : class, IDisposable
        {
            UnsafeHelpers.SkipParamInit(out uniqueLock);

            UniqueLockSemaphore tempUniqueLock = default;
            try
            {
                Result rc = TryAcquireCountSemaphore(out tempUniqueLock, semaphore);
                if (rc.IsFailure()) return rc;

                uniqueLock = new UniqueLockWithPin<T>(ref tempUniqueLock, ref objectToPin);
                return Result.Success;
            }
            finally
            {
                tempUniqueLock.Dispose();
            }
        }

        public static Result RetryFinitelyForTargetLocked(Func<Result> function)
        {
            const int maxRetryCount = 10;
            const int retryWaitTimeMs = 100;

            int remainingRetries = maxRetryCount;

            while (true)
            {
                Result rc = function();

                if (rc.IsSuccess())
                    return rc;

                if (!ResultFs.TargetLocked.Includes(rc))
                    return rc;

                if (remainingRetries <= 0)
                    return rc;

                remainingRetries--;
                Thread.Sleep(retryWaitTimeMs);
            }
        }
    }
}
