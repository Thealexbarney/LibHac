using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Ncm;
using LibHac.Os;
using static LibHac.Fs.Impl.AccessLogStrings;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.Fs.Shim;

/// <summary>
/// Contains functions for mounting content file systems.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0</remarks>
[SkipLocalsInit]
public static class Content
{
    private static FileSystemProxyType ConvertToFileSystemProxyType(ContentType type)
    {
        switch (type)
        {
            case ContentType.Meta: return FileSystemProxyType.Meta;
            case ContentType.Control: return FileSystemProxyType.Control;
            case ContentType.Manual: return FileSystemProxyType.Manual;
            case ContentType.Data: return FileSystemProxyType.Data;
            default:
                Abort.UnexpectedDefault();
                return default;
        }
    }

    private static Result MountContentImpl(FileSystemClient fs, U8Span mountName, U8Span path, ulong id,
        ContentType contentType)
    {
        Result res = fs.Impl.CheckMountNameAcceptingReservedMountName(mountName);
        if (res.IsFailure()) return res.Miss();

        FileSystemProxyType fsType = ConvertToFileSystemProxyType(contentType);

        res = PathUtility.ConvertToFspPath(out FspPath sfPath, path);
        if (res.IsFailure()) return res.Miss();

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var fileSystem = new SharedRef<IFileSystemSf>();

        res = fileSystemProxy.Get.OpenFileSystemWithId(ref fileSystem.Ref(), in sfPath, id, fsType);
        if (res.IsFailure()) return res.Miss();

        using var fileSystemAdapter =
            new UniqueRef<IFileSystem>(new FileSystemServiceObjectAdapter(ref fileSystem.Ref()));

        if (!fileSystemAdapter.HasValue)
            return ResultFs.AllocationMemoryFailedInContentA.Log();

        res = fs.Register(mountName, ref fileSystemAdapter.Ref());
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result MountContent(this FileSystemClient fs, U8Span mountName, U8Span path, ContentType contentType)
    {
        Result res;
        Span<byte> logBuffer = stackalloc byte[0x300];

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = PreMount(contentType);
            Tick end = fs.Hos.Os.GetSystemTick();

            var idString = new IdString();
            var sb = new U8StringBuilder(logBuffer, true);

            sb.Append(LogName).Append(mountName).Append(LogQuote)
                .Append(LogPath).Append(path).Append(LogQuote)
                .Append(LogContentType).Append(idString.ToString(contentType));

            fs.Impl.OutputAccessLogUnlessResultSuccess(res, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            res = PreMount(contentType);
        }

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        ProgramId programId = default;

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = MountContentImpl(fs, mountName, path, programId.Value, contentType);
            Tick end = fs.Hos.Os.GetSystemTick();

            var idString = new IdString();
            var sb = new U8StringBuilder(logBuffer, true);

            sb.Append(LogName).Append(mountName).Append(LogQuote)
                .Append(LogPath).Append(path).Append(LogQuote)
                .Append(LogProgramId).AppendFormat(programId.Value, 'X')
                .Append(LogContentType).Append(idString.ToString(contentType));

            fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            res = MountContentImpl(fs, mountName, path, programId.Value, contentType);
        }

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return Result.Success;

        static Result PreMount(ContentType contentType)
        {
            if (contentType != ContentType.Meta)
                return ResultFs.InvalidArgument.Log();

            return Result.Success;
        }
    }

    public static Result MountContent(this FileSystemClient fs, U8Span mountName, U8Span path, ProgramId programId,
        ContentType contentType)
    {
        Result res;
        Span<byte> logBuffer = stackalloc byte[0x300];

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = MountContentImpl(fs, mountName, path, programId.Value, contentType);
            Tick end = fs.Hos.Os.GetSystemTick();

            var idString = new IdString();
            var sb = new U8StringBuilder(logBuffer, true);

            sb.Append(LogName).Append(mountName).Append(LogQuote)
                .Append(LogPath).Append(path).Append(LogQuote)
                .Append(LogProgramId).AppendFormat(programId.Value, 'X')
                .Append(LogContentType).Append(idString.ToString(contentType));

            logBuffer = sb.Buffer;

            fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(logBuffer));
        }
        else
        {
            res = MountContentImpl(fs, mountName, path, programId.Value, contentType);
        }

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return Result.Success;
    }

    public static Result MountContent(this FileSystemClient fs, U8Span mountName, U8Span path, DataId dataId,
        ContentType contentType)
    {
        Result res;
        Span<byte> logBuffer = stackalloc byte[0x300];

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = MountContentImpl(fs, mountName, path, dataId.Value, contentType);
            Tick end = fs.Hos.Os.GetSystemTick();

            var idString = new IdString();
            var sb = new U8StringBuilder(logBuffer, true);

            sb.Append(LogName).Append(mountName).Append(LogQuote)
                .Append(LogPath).Append(path).Append(LogQuote)
                .Append(LogDataId).AppendFormat(dataId.Value, 'X')
                .Append(LogContentType).Append(idString.ToString(contentType));

            fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            res = MountContentImpl(fs, mountName, path, dataId.Value, contentType);
        }

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return Result.Success;
    }

    public static Result MountContent(this FileSystemClient fs, U8Span mountName, ProgramId programId,
        ContentType contentType)
    {
        Result res;
        Span<byte> logBuffer = stackalloc byte[0x300];

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = Mount(fs, mountName, programId, contentType);
            Tick end = fs.Hos.Os.GetSystemTick();

            var idString = new IdString();
            var sb = new U8StringBuilder(logBuffer, true);

            sb.Append(LogName).Append(mountName).Append(LogQuote)
                .Append(LogProgramId).AppendFormat(programId.Value, 'X')
                .Append(LogContentType).Append(idString.ToString(contentType));

            fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            res = Mount(fs, mountName, programId, contentType);
        }

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return Result.Success;

        static Result Mount(FileSystemClient fs, U8Span mountName, ProgramId programId, ContentType contentType)
        {
            Result res = fs.Impl.CheckMountNameAcceptingReservedMountName(mountName);
            if (res.IsFailure()) return res.Miss();

            FileSystemProxyType fsType = ConvertToFileSystemProxyType(contentType);

            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
            using var fileSystem = new SharedRef<IFileSystemSf>();

            res = fileSystemProxy.Get.OpenFileSystemWithPatch(ref fileSystem.Ref(), programId, fsType);
            if (res.IsFailure()) return res.Miss();

            using var fileSystemAdapter =
                new UniqueRef<IFileSystem>(new FileSystemServiceObjectAdapter(ref fileSystem.Ref()));

            if (!fileSystemAdapter.HasValue)
                return ResultFs.AllocationMemoryFailedInContentA.Log();

            res = fs.Register(mountName, ref fileSystemAdapter.Ref());
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }
    }
}