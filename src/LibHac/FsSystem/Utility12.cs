﻿using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Common;
using LibHac.Fs.Fsa;
using LibHac.Os;

namespace LibHac.FsSystem
{
    /// <summary>
    /// Various utility functions used by the <see cref="LibHac.FsSystem"/> namespace.
    /// </summary>
    /// <remarks>Based on FS 12.0.3 (nnSdk 12.3.1)</remarks>
    internal static class Utility12
    {
        public delegate Result FsIterationTask(in Path path, in DirectoryEntry entry, ref FsIterationTaskClosure closure);

        /// <summary>
        /// Used to pass various ref structs to an <see cref="FsIterationTask"/>.
        /// </summary>
        /// <remarks>
        /// C# does not allow closures over byref-like types. This struct is used as a sort of manual imitation of a closure struct.
        /// It contains various fields that can used if needed to pass references to <see cref="FsIterationTask"/> methods.
        /// The main shortcomings are that every type that might possibly be passed must have a field in the struct.
        /// The struct must also be manually passed through the <see cref="Utility12.IterateDirectoryRecursively"/> method.
        /// And because ref fields aren't as thing as of C# 10, some ref structs may have to be copied into the closure struct. 
        /// </remarks>
        public ref struct FsIterationTaskClosure
        {
            public Span<byte> Buffer;
            public Path DestinationPathBuffer;
            public IFileSystem SourceFileSystem;
            public IFileSystem DestFileSystem;
        }

        private static ReadOnlySpan<byte> RootPath => new[] { (byte)'/' };

        private static Result IterateDirectoryRecursivelyInternal(IFileSystem fs, ref Path workPath,
            ref DirectoryEntry dirEntry, FsIterationTask onEnterDir, FsIterationTask onExitDir, FsIterationTask onFile,
            ref FsIterationTaskClosure closure)
        {
            IDirectory directory = null;
            try
            {
                Result rc = fs.OpenDirectory(out directory, in workPath, OpenDirectoryMode.All);
                if (rc.IsFailure()) return rc;

                while (true)
                {
                    rc = directory.Read(out long entriesRead, SpanHelpers.AsSpan(ref dirEntry));
                    if (rc.IsFailure()) return rc;

                    if (entriesRead == 0)
                        break;

                    workPath.AppendChild(dirEntry.Name);
                    if (rc.IsFailure()) return rc;

                    if (dirEntry.Type == DirectoryEntryType.Directory)
                    {
                        rc = onEnterDir(in workPath, in dirEntry, ref closure);
                        if (rc.IsFailure()) return rc;

                        rc = IterateDirectoryRecursivelyInternal(fs, ref workPath, ref dirEntry, onEnterDir, onExitDir,
                            onFile, ref closure);
                        if (rc.IsFailure()) return rc;

                        rc = onExitDir(in workPath, in dirEntry, ref closure);
                        if (rc.IsFailure()) return rc;
                    }
                    else
                    {
                        rc = onFile(in workPath, in dirEntry, ref closure);
                        if (rc.IsFailure()) return rc;
                    }

                    rc = workPath.RemoveChild();
                    if (rc.IsFailure()) return rc;
                }

                return Result.Success;
            }
            finally
            {
                directory?.Dispose();
            }
        }

        private static Result CleanupDirectoryRecursivelyInternal(IFileSystem fs, ref Path workPath,
            ref DirectoryEntry dirEntry, FsIterationTask onEnterDir, FsIterationTask onExitDir, FsIterationTask onFile,
            ref FsIterationTaskClosure closure)
        {
            IDirectory directory = null;
            try
            {
                while (true)
                {
                    Result rc = fs.OpenDirectory(out directory, in workPath, OpenDirectoryMode.All);
                    if (rc.IsFailure()) return rc;

                    rc = directory.Read(out long entriesRead, SpanHelpers.AsSpan(ref dirEntry));
                    if (rc.IsFailure()) return rc;

                    directory.Dispose();
                    directory = null;

                    if (entriesRead == 0)
                        break;

                    rc = workPath.AppendChild(dirEntry.Name);
                    if (rc.IsFailure()) return rc;

                    if (dirEntry.Type == DirectoryEntryType.Directory)
                    {
                        rc = onEnterDir(in workPath, in dirEntry, ref closure);
                        if (rc.IsFailure()) return rc;

                        rc = CleanupDirectoryRecursivelyInternal(fs, ref workPath, ref dirEntry, onEnterDir, onExitDir,
                            onFile, ref closure);
                        if (rc.IsFailure()) return rc;

                        rc = onExitDir(in workPath, in dirEntry, ref closure);
                        if (rc.IsFailure()) return rc;
                    }
                    else
                    {
                        rc = onFile(in workPath, in dirEntry, ref closure);
                        if (rc.IsFailure()) return rc;
                    }

                    rc = workPath.RemoveChild();
                    if (rc.IsFailure()) return rc;
                }

                return Result.Success;
            }
            finally
            {
                directory?.Dispose();
            }
        }

        public static Result IterateDirectoryRecursively(IFileSystem fs, in Path rootPath, ref DirectoryEntry dirEntry,
            FsIterationTask onEnterDir, FsIterationTask onExitDir, FsIterationTask onFile,
            ref FsIterationTaskClosure closure)
        {
            using var pathBuffer = new Path();
            Result rc = pathBuffer.Initialize(in rootPath);
            if (rc.IsFailure()) return rc;

            rc = IterateDirectoryRecursivelyInternal(fs, ref pathBuffer.Ref(), ref dirEntry, onEnterDir, onExitDir,
                onFile, ref closure);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        public static Result CleanupDirectoryRecursively(IFileSystem fs, in Path rootPath, ref DirectoryEntry dirEntry,
            FsIterationTask onEnterDir, FsIterationTask onExitDir, FsIterationTask onFile,
            ref FsIterationTaskClosure closure)
        {
            using var pathBuffer = new Path();
            Result rc = pathBuffer.Initialize(in rootPath);
            if (rc.IsFailure()) return rc;

            return CleanupDirectoryRecursivelyInternal(fs, ref pathBuffer.Ref(), ref dirEntry, onEnterDir, onExitDir, onFile,
                ref closure);
        }

        public static Result CopyFile(IFileSystem destFileSystem, IFileSystem sourceFileSystem, in Path destPath,
            in Path sourcePath, Span<byte> workBuffer)
        {
            // Open source file.
            Result rc = sourceFileSystem.OpenFile(out IFile sourceFile, sourcePath, OpenMode.Read);
            if (rc.IsFailure()) return rc;

            using (sourceFile)
            {
                rc = sourceFile.GetSize(out long fileSize);
                if (rc.IsFailure()) return rc;

                rc = destFileSystem.CreateFile(in destPath, fileSize);
                if (rc.IsFailure()) return rc;

                rc = destFileSystem.OpenFile(out IFile destFile, in destPath, OpenMode.Write);
                if (rc.IsFailure()) return rc;

                using (destFile)
                {
                    // Read/Write file in work buffer sized chunks.
                    long remaining = fileSize;
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

        public static Result CopyDirectoryRecursively(IFileSystem destinationFileSystem, IFileSystem sourceFileSystem,
            in Path destinationPath, in Path sourcePath, ref DirectoryEntry dirEntry, Span<byte> workBuffer)
        {
            static Result OnEnterDir(in Path path, in DirectoryEntry entry, ref FsIterationTaskClosure closure)
            {
                Result rc = closure.DestinationPathBuffer.AppendChild(entry.Name);
                if (rc.IsFailure()) return rc;

                return closure.SourceFileSystem.CreateDirectory(in closure.DestinationPathBuffer);
            }

            static Result OnExitDir(in Path path, in DirectoryEntry entry, ref FsIterationTaskClosure closure)
            {
                return closure.DestinationPathBuffer.RemoveChild();
            }

            static Result OnFile(in Path path, in DirectoryEntry entry, ref FsIterationTaskClosure closure)
            {
                Result rc = closure.DestinationPathBuffer.AppendChild(entry.Name);
                if (rc.IsFailure()) return rc;

                rc = CopyFile(closure.DestFileSystem, closure.SourceFileSystem, in closure.DestinationPathBuffer,
                    in path, closure.Buffer);
                if (rc.IsFailure()) return rc;

                return closure.DestinationPathBuffer.RemoveChild();
            }

            var closure = new FsIterationTaskClosure();
            closure.Buffer = workBuffer;
            closure.SourceFileSystem = sourceFileSystem;
            closure.DestFileSystem = destinationFileSystem;

            Result rc = closure.DestinationPathBuffer.Initialize(destinationPath);
            if (rc.IsFailure()) return rc;

            rc = IterateDirectoryRecursively(sourceFileSystem, in sourcePath, ref dirEntry, OnEnterDir, OnExitDir,
                OnFile, ref closure);

            closure.DestinationPathBuffer.Dispose();
            return rc;
        }

        public static Result CopyDirectoryRecursively(IFileSystem fileSystem, in Path destinationPath,
            in Path sourcePath, ref DirectoryEntry dirEntry, Span<byte> workBuffer)
        {
            var closure = new FsIterationTaskClosure();
            closure.Buffer = workBuffer;
            closure.SourceFileSystem = fileSystem;

            Result rc = closure.DestinationPathBuffer.Initialize(destinationPath);
            if (rc.IsFailure()) return rc;

            static Result OnEnterDir(in Path path, in DirectoryEntry entry, ref FsIterationTaskClosure closure)
            {
                Result rc = closure.DestinationPathBuffer.AppendChild(entry.Name);
                if (rc.IsFailure()) return rc;

                return closure.SourceFileSystem.CreateDirectory(in closure.DestinationPathBuffer);
            }

            static Result OnExitDir(in Path path, in DirectoryEntry entry, ref FsIterationTaskClosure closure)
            {
                return closure.DestinationPathBuffer.RemoveChild();
            }

            static Result OnFile(in Path path, in DirectoryEntry entry, ref FsIterationTaskClosure closure)
            {
                Result rc = closure.DestinationPathBuffer.AppendChild(entry.Name);
                if (rc.IsFailure()) return rc;

                rc = CopyFile(closure.SourceFileSystem, closure.SourceFileSystem, in closure.DestinationPathBuffer,
                    in path, closure.Buffer);
                if (rc.IsFailure()) return rc;

                return closure.DestinationPathBuffer.RemoveChild();
            }

            rc = IterateDirectoryRecursively(fileSystem, in sourcePath, ref dirEntry, OnEnterDir, OnExitDir, OnFile,
                ref closure);

            closure.DestinationPathBuffer.Dispose();
            return rc;
        }

        public static Result VerifyDirectoryRecursively(IFileSystem fileSystem, Span<byte> workBuffer)
        {
            static Result OnEnterAndExitDir(in Path path, in DirectoryEntry entry, ref FsIterationTaskClosure closure) =>
                Result.Success;

            static Result OnFile(in Path path, in DirectoryEntry entry, ref FsIterationTaskClosure closure)
            {
                IFile file = null;
                try
                {
                    Result rc = closure.SourceFileSystem.OpenFile(out file, in path, OpenMode.Read);
                    if (rc.IsFailure()) return rc;

                    long offset = 0;

                    while (true)
                    {
                        rc = file.Read(out long bytesRead, offset, closure.Buffer, ReadOption.None);
                        if (rc.IsFailure()) return rc;

                        if (bytesRead < closure.Buffer.Length)
                            break;

                        offset += bytesRead;
                    }

                    return Result.Success;
                }
                finally
                {
                    file?.Dispose();
                }
            }

            using var rootPath = new Path();
            Result rc = PathFunctions.SetUpFixedPath(ref rootPath.Ref(), RootPath);
            if (rc.IsFailure()) return rc;

            var closure = new FsIterationTaskClosure();
            closure.Buffer = workBuffer;
            closure.SourceFileSystem = fileSystem;

            var dirEntryBuffer = new DirectoryEntry();

            return IterateDirectoryRecursively(fileSystem, in rootPath, ref dirEntryBuffer, OnEnterAndExitDir,
                OnEnterAndExitDir, OnFile, ref closure);
        }

        private static Result EnsureDirectoryImpl(IFileSystem fileSystem, in Path path)
        {
            using var pathCopy = new Path();
            bool isFinished;

            Result rc = pathCopy.Initialize(in path);
            if (rc.IsFailure()) return rc;

            using var parser = new DirectoryPathParser();
            rc = parser.Initialize(ref pathCopy.Ref());
            if (rc.IsFailure()) return rc;

            do
            {
                // Check if the path exists
                rc = fileSystem.GetEntryType(out DirectoryEntryType type, in parser.CurrentPath);
                if (!rc.IsSuccess())
                {
                    // Something went wrong if we get a result other than PathNotFound
                    if (!ResultFs.PathNotFound.Includes(rc))
                        return rc;

                    // Create the directory
                    rc = fileSystem.CreateDirectory(in parser.CurrentPath);
                    if (rc.IsFailure() && !ResultFs.PathAlreadyExists.Includes(rc))
                        return rc;

                    // Check once more if the path exists
                    rc = fileSystem.GetEntryType(out type, in parser.CurrentPath);
                    if (rc.IsFailure()) return rc;
                }

                if (type == DirectoryEntryType.File)
                    return ResultFs.PathAlreadyExists.Log();

                rc = parser.ReadNext(out isFinished);
                if (rc.IsFailure()) return rc;
            } while (!isFinished);

            return Result.Success;
        }

        public static Result EnsureDirectory(IFileSystem fileSystem, in Path path)
        {
            Result rc = fileSystem.GetEntryType(out _, in path);

            if (!rc.IsSuccess())
            {
                if (!ResultFs.PathNotFound.Includes(rc))
                    return rc;

                rc = EnsureDirectoryImpl(fileSystem, in path);
                if (rc.IsFailure()) return rc;
            }

            return Result.Success;
        }

        public static void AddCounter(Span<byte> counter, ulong value)
        {
            const int bitsPerByte = 8;

            ulong remaining = value;
            byte carry = 0;

            for (int i = 0; i < counter.Length; i++)
            {
                int sum = counter[counter.Length - 1 - i] + (byte)remaining + carry;
                carry = (byte)(sum >> bitsPerByte);

                counter[counter.Length - 1 - i] = (byte)sum;

                remaining >>= bitsPerByte;

                if (carry == 0 && remaining == 0)
                    break;
            }
        }

        public static Result TryAcquireCountSemaphore(out UniqueLock<SemaphoreAdapter> uniqueLock, SemaphoreAdapter semaphore)
        {
            UniqueLock<SemaphoreAdapter> tempUniqueLock = default;
            try
            {
                tempUniqueLock = new UniqueLock<SemaphoreAdapter>(semaphore, new DeferLock());

                if (!tempUniqueLock.TryLock())
                {
                    uniqueLock = default;
                    return ResultFs.OpenCountLimit.Log();
                }

                uniqueLock = Shared.Move(ref tempUniqueLock);
                return Result.Success;
            }
            finally
            {
                tempUniqueLock.Dispose();
            }
        }

        public static Result MakeUniqueLockWithPin<T>(out IUniqueLock uniqueLock, SemaphoreAdapter semaphore,
            ref ReferenceCountedDisposable<T> objectToPin) where T : class, IDisposable
        {
            UnsafeHelpers.SkipParamInit(out uniqueLock);

            UniqueLock<SemaphoreAdapter> tempUniqueLock = default;
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
    }
}
