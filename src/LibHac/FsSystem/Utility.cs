using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSrv.Sf;
using LibHac.Os;
using LibHac.Sf;
using LibHac.Util;
using IDirectory = LibHac.Fs.Fsa.IDirectory;
using IFile = LibHac.Fs.Fsa.IFile;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using Path = LibHac.Fs.Path;

namespace LibHac.FsSystem;

public class DummyEventNotifier : IEventNotifier
{
    public void Dispose() { }

    public Result GetEventHandle(out NativeHandle handle)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Various utility functions used by the <see cref="LibHac.FsSystem"/> namespace.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
internal static class Utility
{
    public delegate Result FsIterationTask(ref readonly Path path, in DirectoryEntry entry, ref FsIterationTaskClosure closure);

    /// <summary>
    /// Used to pass various ref structs to an <see cref="FsIterationTask"/>.
    /// </summary>
    /// <remarks>
    /// C# does not allow closures over byref-like types. This struct is used as a sort of manual imitation of a closure struct.
    /// It contains various fields that can used if needed to pass references to <see cref="FsIterationTask"/> methods.
    /// The main shortcomings are that every type that might possibly be passed must have a field in the struct.
    /// The struct must also be manually passed through the <see cref="Utility.IterateDirectoryRecursively"/> method.
    /// And because ref fields to ref structs aren't as thing as of C# 11, some ref structs may have to be copied into the closure struct. 
    /// </remarks>
    [NonCopyable]
    public ref struct FsIterationTaskClosure
    {
        public Span<byte> Buffer;
        public Path DestinationPathBuffer;
        public IFileSystem SourceFileSystem;
        public IFileSystem DestFileSystem;
    }

    private static ReadOnlySpan<byte> RootPath => "/"u8;

    private static Result IterateDirectoryRecursivelyInternal(IFileSystem fs, ref Path workPath,
        ref DirectoryEntry dirEntry, FsIterationTask onEnterDir, FsIterationTask onExitDir, FsIterationTask onFile,
        ref FsIterationTaskClosure closure)
    {
        using var directory = new UniqueRef<IDirectory>();

        Result res = fs.OpenDirectory(ref directory.Ref, in workPath, OpenDirectoryMode.All);
        if (res.IsFailure()) return res.Miss();

        while (true)
        {
            res = directory.Get.Read(out long entriesRead, SpanHelpers.AsSpan(ref dirEntry));
            if (res.IsFailure()) return res.Miss();

            if (entriesRead == 0)
                break;

            workPath.AppendChild(dirEntry.Name);
            if (res.IsFailure()) return res.Miss();

            if (dirEntry.Type == DirectoryEntryType.Directory)
            {
                res = onEnterDir(in workPath, in dirEntry, ref closure);
                if (res.IsFailure()) return res.Miss();

                res = IterateDirectoryRecursivelyInternal(fs, ref workPath, ref dirEntry, onEnterDir, onExitDir,
                    onFile, ref closure);
                if (res.IsFailure()) return res.Miss();

                res = onExitDir(in workPath, in dirEntry, ref closure);
                if (res.IsFailure()) return res.Miss();
            }
            else
            {
                res = onFile(in workPath, in dirEntry, ref closure);
                if (res.IsFailure()) return res.Miss();
            }

            res = workPath.RemoveChild();
            if (res.IsFailure()) return res.Miss();
        }

        return Result.Success;
    }

    private static Result CleanupDirectoryRecursivelyInternal(IFileSystem fs, ref Path workPath,
        ref DirectoryEntry dirEntry, FsIterationTask onEnterDir, FsIterationTask onExitDir, FsIterationTask onFile,
        ref FsIterationTaskClosure closure)
    {
        using var directory = new UniqueRef<IDirectory>();

        while (true)
        {
            Result res = fs.OpenDirectory(ref directory.Ref, in workPath, OpenDirectoryMode.All);
            if (res.IsFailure()) return res.Miss();

            res = directory.Get.Read(out long entriesRead, SpanHelpers.AsSpan(ref dirEntry));
            if (res.IsFailure()) return res.Miss();

            directory.Reset();

            if (entriesRead == 0)
                break;

            res = workPath.AppendChild(dirEntry.Name);
            if (res.IsFailure()) return res.Miss();

            if (dirEntry.Type == DirectoryEntryType.Directory)
            {
                res = onEnterDir(in workPath, in dirEntry, ref closure);
                if (res.IsFailure()) return res.Miss();

                res = CleanupDirectoryRecursivelyInternal(fs, ref workPath, ref dirEntry, onEnterDir, onExitDir,
                    onFile, ref closure);
                if (res.IsFailure()) return res.Miss();

                res = onExitDir(in workPath, in dirEntry, ref closure);
                if (res.IsFailure()) return res.Miss();
            }
            else
            {
                res = onFile(in workPath, in dirEntry, ref closure);
                if (res.IsFailure()) return res.Miss();
            }

            res = workPath.RemoveChild();
            if (res.IsFailure()) return res.Miss();
        }

        return Result.Success;
    }

    public static Result IterateDirectoryRecursively(IFileSystem fs, ref readonly Path rootPath,
        ref DirectoryEntry dirEntry, FsIterationTask onEnterDir, FsIterationTask onExitDir, FsIterationTask onFile,
        ref FsIterationTaskClosure closure)
    {
        using var pathBuffer = new Path();
        Result res = pathBuffer.Initialize(in rootPath);
        if (res.IsFailure()) return res.Miss();

        res = IterateDirectoryRecursivelyInternal(fs, ref pathBuffer.Ref(), ref dirEntry, onEnterDir, onExitDir,
            onFile, ref closure);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result CleanupDirectoryRecursively(IFileSystem fs, ref readonly Path rootPath,
        ref DirectoryEntry dirEntry, FsIterationTask onEnterDir, FsIterationTask onExitDir, FsIterationTask onFile,
        ref FsIterationTaskClosure closure)
    {
        using var pathBuffer = new Path();
        Result res = pathBuffer.Initialize(in rootPath);
        if (res.IsFailure()) return res.Miss();

        return CleanupDirectoryRecursivelyInternal(fs, ref pathBuffer.Ref(), ref dirEntry, onEnterDir, onExitDir, onFile,
            ref closure);
    }

    public static Result CopyFile(IFileSystem destFileSystem, IFileSystem sourceFileSystem, ref readonly Path destPath,
        ref readonly Path sourcePath, Span<byte> workBuffer)
    {
        // Open source file.
        using var sourceFile = new UniqueRef<IFile>();
        Result res = sourceFileSystem.OpenFile(ref sourceFile.Ref, in sourcePath, OpenMode.Read);
        if (res.IsFailure()) return res.Miss();

        res = sourceFile.Get.GetSize(out long fileSize);
        if (res.IsFailure()) return res.Miss();

        using var destFile = new UniqueRef<IFile>();
        res = destFileSystem.CreateFile(in destPath, fileSize);
        if (res.IsFailure()) return res.Miss();

        res = destFileSystem.OpenFile(ref destFile.Ref, in destPath, OpenMode.Write);
        if (res.IsFailure()) return res.Miss();

        // Read/Write file in work-buffer-sized chunks.
        long remaining = fileSize;
        long offset = 0;

        while (remaining > 0)
        {
            res = sourceFile.Get.Read(out long bytesRead, offset, workBuffer, ReadOption.None);
            if (res.IsFailure()) return res.Miss();

            res = destFile.Get.Write(offset, workBuffer.Slice(0, (int)bytesRead), WriteOption.None);
            if (res.IsFailure()) return res.Miss();

            remaining -= bytesRead;
            offset += bytesRead;
        }

        return Result.Success;
    }

    public static Result CopyDirectoryRecursively(IFileSystem destinationFileSystem, IFileSystem sourceFileSystem,
        ref readonly Path destinationPath, ref readonly Path sourcePath, ref DirectoryEntry dirEntry,
        Span<byte> workBuffer)
    {
        static Result OnEnterDir(ref readonly Path path, in DirectoryEntry entry, ref FsIterationTaskClosure closure)
        {
            Result res = closure.DestinationPathBuffer.AppendChild(entry.Name);
            if (res.IsFailure()) return res.Miss();

            return closure.SourceFileSystem.CreateDirectory(in closure.DestinationPathBuffer);
        }

        static Result OnExitDir(ref readonly Path path, in DirectoryEntry entry, ref FsIterationTaskClosure closure)
        {
            return closure.DestinationPathBuffer.RemoveChild();
        }

        static Result OnFile(ref readonly Path path, in DirectoryEntry entry, ref FsIterationTaskClosure closure)
        {
            Result res = closure.DestinationPathBuffer.AppendChild(entry.Name);
            if (res.IsFailure()) return res.Miss();

            res = CopyFile(closure.DestFileSystem, closure.SourceFileSystem, in closure.DestinationPathBuffer,
                in path, closure.Buffer);
            if (res.IsFailure()) return res.Miss();

            return closure.DestinationPathBuffer.RemoveChild();
        }

        var closure = new FsIterationTaskClosure();
        closure.Buffer = workBuffer;
        closure.SourceFileSystem = sourceFileSystem;
        closure.DestFileSystem = destinationFileSystem;

        Result res = closure.DestinationPathBuffer.Initialize(in destinationPath);
        if (res.IsFailure()) return res.Miss();

        res = IterateDirectoryRecursively(sourceFileSystem, in sourcePath, ref dirEntry, OnEnterDir, OnExitDir,
            OnFile, ref closure);

        closure.DestinationPathBuffer.Dispose();
        return res;
    }

    public static Result CopyDirectoryRecursively(IFileSystem fileSystem, ref readonly Path destinationPath,
        ref readonly Path sourcePath, ref DirectoryEntry dirEntry, Span<byte> workBuffer)
    {
        var closure = new FsIterationTaskClosure();
        closure.Buffer = workBuffer;
        closure.SourceFileSystem = fileSystem;

        Result res = closure.DestinationPathBuffer.Initialize(in destinationPath);
        if (res.IsFailure()) return res.Miss();

        static Result OnEnterDir(ref readonly Path path, in DirectoryEntry entry, ref FsIterationTaskClosure closure)
        {
            Result res = closure.DestinationPathBuffer.AppendChild(entry.Name);
            if (res.IsFailure()) return res.Miss();

            return closure.SourceFileSystem.CreateDirectory(in closure.DestinationPathBuffer);
        }

        static Result OnExitDir(ref readonly Path path, in DirectoryEntry entry, ref FsIterationTaskClosure closure)
        {
            return closure.DestinationPathBuffer.RemoveChild();
        }

        static Result OnFile(ref readonly Path path, in DirectoryEntry entry, ref FsIterationTaskClosure closure)
        {
            Result res = closure.DestinationPathBuffer.AppendChild(entry.Name);
            if (res.IsFailure()) return res.Miss();

            res = CopyFile(closure.SourceFileSystem, closure.SourceFileSystem, in closure.DestinationPathBuffer,
                in path, closure.Buffer);
            if (res.IsFailure()) return res.Miss();

            return closure.DestinationPathBuffer.RemoveChild();
        }

        res = IterateDirectoryRecursively(fileSystem, in sourcePath, ref dirEntry, OnEnterDir, OnExitDir, OnFile,
            ref closure);

        closure.DestinationPathBuffer.Dispose();
        return res;
    }

    public static Result VerifyDirectoryRecursively(IFileSystem fileSystem, Span<byte> workBuffer)
    {
        static Result OnEnterAndExitDir(ref readonly Path path, in DirectoryEntry entry, ref FsIterationTaskClosure closure) =>
            Result.Success;

        static Result OnFile(ref readonly Path path, in DirectoryEntry entry, ref FsIterationTaskClosure closure)
        {
            using var file = new UniqueRef<IFile>();

            Result res = closure.SourceFileSystem.OpenFile(ref file.Ref, in path, OpenMode.Read);
            if (res.IsFailure()) return res.Miss();

            long offset = 0;

            while (true)
            {
                res = file.Get.Read(out long bytesRead, offset, closure.Buffer, ReadOption.None);
                if (res.IsFailure()) return res.Miss();

                if (bytesRead < closure.Buffer.Length)
                    break;

                offset += bytesRead;
            }

            return Result.Success;
        }

        using var rootPath = new Path();
        Result res = PathFunctions.SetUpFixedPath(ref rootPath.Ref(), RootPath);
        if (res.IsFailure()) return res.Miss();

        var closure = new FsIterationTaskClosure();
        closure.Buffer = workBuffer;
        closure.SourceFileSystem = fileSystem;

        var dirEntryBuffer = new DirectoryEntry();

        return IterateDirectoryRecursively(fileSystem, in rootPath, ref dirEntryBuffer, OnEnterAndExitDir,
            OnEnterAndExitDir, OnFile, ref closure);
    }

    private static Result EnsureDirectoryImpl(IFileSystem fileSystem, ref readonly Path path)
    {
        using var pathCopy = new Path();
        bool isFinished;

        Result res = pathCopy.Initialize(in path);
        if (res.IsFailure()) return res.Miss();

        using var parser = new DirectoryPathParser();
        res = parser.Initialize(ref pathCopy.Ref());
        if (res.IsFailure()) return res.Miss();

        do
        {
            // Check if the path exists
            res = fileSystem.GetEntryType(out DirectoryEntryType type, in parser.GetCurrentPath());
            if (!res.IsSuccess())
            {
                // Something went wrong if we get a result other than PathNotFound
                if (!ResultFs.PathNotFound.Includes(res))
                    return res;

                // Create the directory
                res = fileSystem.CreateDirectory(in parser.GetCurrentPath());
                if (res.IsFailure() && !ResultFs.PathAlreadyExists.Includes(res))
                    return res;

                // Check once more if the path exists
                res = fileSystem.GetEntryType(out type, in parser.GetCurrentPath());
                if (res.IsFailure()) return res.Miss();
            }

            if (type == DirectoryEntryType.File)
                return ResultFs.PathAlreadyExists.Log();

            res = parser.ReadNext(out isFinished);
            if (res.IsFailure()) return res.Miss();
        } while (!isFinished);

        return Result.Success;
    }

    public static Result EnsureDirectory(IFileSystem fileSystem, ref readonly Path path)
    {
        Result res = fileSystem.GetEntryType(out _, in path);

        if (!res.IsSuccess())
        {
            if (!ResultFs.PathNotFound.Includes(res))
                return res;

            res = EnsureDirectoryImpl(fileSystem, in path);
            if (res.IsFailure()) return res.Miss();
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

    public static Result TryAcquireCountSemaphore(ref UniqueLock<SemaphoreAdapter> outUniqueLock,
        SemaphoreAdapter semaphore)
    {
        using var uniqueLock = new UniqueLock<SemaphoreAdapter>(semaphore, new DeferLock());

        if (!uniqueLock.TryLock())
            return ResultFs.OpenCountLimit.Log();

        outUniqueLock.Set(ref uniqueLock.Ref());
        return Result.Success;
    }

    public static Result MakeUniqueLockWithPin<T>(ref UniqueRef<IUniqueLock> outUniqueLock,
        SemaphoreAdapter semaphore, ref SharedRef<T> objectToPin) where T : class, IDisposable
    {
        using var semaphoreAdapter = new UniqueLock<SemaphoreAdapter>();
        Result res = TryAcquireCountSemaphore(ref semaphoreAdapter.Ref(), semaphore);
        if (res.IsFailure()) return res.Miss();

        var lockWithPin = new UniqueLockWithPin<T>(ref semaphoreAdapter.Ref(), ref objectToPin);
        using var uniqueLock = new UniqueRef<IUniqueLock>(lockWithPin);

        outUniqueLock.Set(ref uniqueLock.Ref);
        return Result.Success;
    }

    private static void SubtractIfHasValue(ref int inOutValue, int count, bool hasValue)
    {
        if (hasValue)
        {
            inOutValue -= count;
            Assert.SdkAssert(inOutValue >= 0);
            inOutValue = Math.Max(inOutValue, 0);
        }
    }

    private static ulong GetCodePointByteLength(byte firstUtf8CodeUnit)
    {
        if ((firstUtf8CodeUnit & 0x80) == 0)
            return 1;

        if ((firstUtf8CodeUnit & 0xE0) == 0xC0)
            return 2;

        if ((firstUtf8CodeUnit & 0xF0) == 0xE0)
            return 3;

        if ((firstUtf8CodeUnit & 0xF8) == 0xF0)
            return 4;

        return 0;
    }

    public static void SubtractAllPathLengthMax(ref FileSystemAttribute attribute, int count)
    {
        SubtractIfHasValue(ref attribute.DirectoryPathLengthMax, count, attribute.DirectoryPathLengthMaxHasValue);
        SubtractIfHasValue(ref attribute.FilePathLengthMax, count, attribute.FilePathLengthMaxHasValue);
    }

    public static Result CountUtf16CharacterForUtf8String(out ulong outCount, ReadOnlySpan<byte> utf8String)
    {
        UnsafeHelpers.SkipParamInit(out outCount);

        Span<byte> buffer = stackalloc byte[4];
        ReadOnlySpan<byte> curString = utf8String;
        ulong utf16CodeUnitTotalCount = 0;

        while (curString.Length > 0 && curString[0] != 0)
        {
            int utf16CodeUnitCount = GetCodePointByteLength(curString[0]) >= 4 ? 2 : 1;
            buffer.Clear();

            if (CharacterEncoding.PickOutCharacterFromUtf8String(buffer, ref curString) != CharacterEncodingResult.Success)
            {
                return ResultFs.InvalidPathFormat.Log();
            }

            utf16CodeUnitTotalCount += (ulong)utf16CodeUnitCount;
        }

        outCount = utf16CodeUnitTotalCount;
        return Result.Success;
    }

    public static void SubtractAllUtf16CountMax(ref FileSystemAttribute attribute, int count)
    {
        SubtractIfHasValue(ref attribute.Utf16DirectoryPathLengthMax, count, attribute.Utf16DirectoryPathLengthMaxHasValue);
        SubtractIfHasValue(ref attribute.Utf16FilePathLengthMax, count, attribute.Utf16FilePathLengthMaxHasValue);
        SubtractIfHasValue(ref attribute.Utf16CreateDirectoryPathLengthMax, count, attribute.Utf16CreateDirectoryPathLengthMaxHasValue);
        SubtractIfHasValue(ref attribute.Utf16DeleteDirectoryPathLengthMax, count, attribute.Utf16DeleteDirectoryPathLengthMaxHasValue);
        SubtractIfHasValue(ref attribute.Utf16RenameSourceDirectoryPathLengthMax, count, attribute.Utf16RenameSourceDirectoryPathLengthMaxHasValue);
        SubtractIfHasValue(ref attribute.Utf16RenameDestinationDirectoryPathLengthMax, count, attribute.Utf16RenameDestinationDirectoryPathLengthMaxHasValue);
        SubtractIfHasValue(ref attribute.Utf16OpenDirectoryPathLengthMax, count, attribute.Utf16OpenDirectoryPathLengthMaxHasValue);
    }
}