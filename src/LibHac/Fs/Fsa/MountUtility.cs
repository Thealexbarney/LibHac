using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Impl;
using LibHac.Fs.Shim;
using LibHac.Os;
using LibHac.Util;
using static LibHac.Fs.Impl.AccessLogStrings;
using static LibHac.Fs.Impl.CommonMountNames;
using static LibHac.Fs.StringTraits;

namespace LibHac.Fs.Fsa;

/// <summary>
/// Contains functions for managing mounted file systems.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
public static class MountUtility
{
    /// <summary>
    /// Gets the mount name and non-mounted path components from a path that has a mount name.
    /// </summary>
    /// <param name="mountName">If the method returns successfully, contains the mount name of the provided path;
    /// otherwise the contents are undefined.</param>
    /// <param name="outSubPath">If the method returns successfully, contains the provided path without the
    /// mount name; otherwise the contents are undefined.</param>
    /// <param name="path">The <see cref="Path"/> to process.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.InvalidPathFormat"/>: <paramref name="path"/> does not contain a sub path after
    /// the mount name that begins with <c>/</c> or <c>\</c>.<br/>
    /// <see cref="ResultFs.InvalidMountName"/>: <paramref name="path"/> contains an invalid mount name
    /// or does not have a mount name.</returns>
    private static Result GetMountNameAndSubPath(out MountName mountName, out U8Span outSubPath, U8Span path)
    {
        UnsafeHelpers.SkipParamInit(out mountName);
        outSubPath = default;

        if (WindowsPath.IsWindowsDrive(path) || WindowsPath.IsUncPath(path))
        {
            StringUtils.Copy(mountName.Name, HostRootFileSystemMountName);
            mountName.Name[PathTool.MountNameLengthMax] = NullTerminator;

            outSubPath = path;
            return Result.Success;
        }

        int mountLen = FindMountNameDriveSeparator(path);

        if (mountLen == 0)
            return ResultFs.InvalidMountName.Log();

        if (mountLen > PathTool.MountNameLengthMax)
            return ResultFs.InvalidMountName.Log();

        if (mountLen <= 0)
            return ResultFs.InvalidMountName.Log();

        U8Span subPath = path.Slice(mountLen + 1);

        bool startsWithDir = subPath.Length > 0 &&
                             (subPath[0] == DirectorySeparator || subPath[0] == AltDirectorySeparator);

        if (!startsWithDir)
            return ResultFs.InvalidPathFormat.Log();

        path.Value.Slice(0, mountLen).CopyTo(mountName.Name);
        mountName.Name[mountLen] = NullTerminator;

        outSubPath = subPath;
        return Result.Success;

        static int FindMountNameDriveSeparator(U8Span path)
        {
            for (int i = 0; i < path.Length && i < PathTool.MountNameLengthMax + 1; i++)
            {
                if (path[i] == NullTerminator)
                    return 0;

                if (path[i] == DriveSeparator)
                    return i;
            }

            return 0;
        }
    }

    public static bool IsValidMountName(this FileSystemClientImpl fs, U8Span name)
    {
        if (name.IsEmpty())
            return false;

        // Check for a single-letter mount name
        if ((name.Length <= 1 || name[1] == 0) &&
            ('a' <= name[0] && name[0] <= 'z' || 'A' <= name[0] && name[0] <= 'Z'))
        {
            return false;
        }

        // Check for mount or directory separators
        int length = 0;
        for (int i = 0; i < name.Length && name[i] != 0; i++)
        {
            if (name[i] == DriveSeparator || name[i] == DirectorySeparator)
                return false;

            if (++length > PathTool.MountNameLengthMax)
                return false;
        }

        return Utf8StringUtil.VerifyUtf8String(name);
    }

    public static bool IsUsedReservedMountName(this FileSystemClientImpl fs, U8Span name)
    {
        return name.Length > 0 && name[0] == ReservedMountNamePrefixCharacter;
    }

    internal static Result FindFileSystem(this FileSystemClientImpl fs, out FileSystemAccessor fileSystem,
        out U8Span subPath, U8Span path)
    {
        UnsafeHelpers.SkipParamInit(out fileSystem);
        subPath = default;

        if (path.IsNull())
            return ResultFs.NullptrArgument.Log();

        int hostMountNameLen = StringUtils.GetLength(HostRootFileSystemMountName);
        if (StringUtils.Compare(path, HostRootFileSystemMountName, hostMountNameLen) == 0)
        {
            return ResultFs.NotMounted.Log();
        }

        Result res = GetMountNameAndSubPath(out MountName mountName, out subPath, path);
        if (res.IsFailure()) return res.Miss();

        return fs.Find(out fileSystem, new U8Span(mountName.Name));
    }

    public static Result CheckMountName(this FileSystemClientImpl fs, U8Span name)
    {
        if (name.IsNull())
            return ResultFs.NullptrArgument.Log();

        if (fs.IsUsedReservedMountName(name))
            return ResultFs.InvalidMountName.Log();

        if (!fs.IsValidMountName(name))
            return ResultFs.InvalidMountName.Log();

        return Result.Success;
    }

    public static Result CheckMountNameAcceptingReservedMountName(this FileSystemClientImpl fs, U8Span name)
    {
        if (name.IsNull())
            return ResultFs.NullptrArgument.Log();

        if (!fs.IsValidMountName(name))
            return ResultFs.InvalidMountName.Log();

        return Result.Success;
    }

    public static Result Unmount(this FileSystemClientImpl fs, U8Span mountName)
    {
        Result res = fs.Find(out FileSystemAccessor fileSystem, mountName);
        if (res.IsFailure()) return res.Miss();

        if (fileSystem.IsFileDataCacheAttachable())
        {
            using var fileDataCacheAccessor = new GlobalFileDataCacheAccessorReadableScopedPointer();

            if (fs.TryGetGlobalFileDataCacheAccessor(ref Unsafe.AsRef(in fileDataCacheAccessor)))
            {
                fileSystem.PurgeFileDataCache(fileDataCacheAccessor.Get());
            }
        }

        fs.Unregister(mountName);
        return Result.Success;
    }

    public static Result IsMounted(this FileSystemClientImpl fs, out bool isMounted, U8Span mountName)
    {
        UnsafeHelpers.SkipParamInit(out isMounted);

        Result res = fs.Find(out _, mountName);
        if (res.IsFailure())
        {
            if (!ResultFs.NotMounted.Includes(res))
                return res;

            isMounted = false;
        }
        else
        {
            isMounted = true;
        }

        return Result.Success;
    }

    public static void Unmount(this FileSystemClient fs, U8Span mountName)
    {
        Result res;
        Span<byte> logBuffer = stackalloc byte[0x30];

        if (fs.Impl.IsEnabledAccessLog() && fs.Impl.IsEnabledFileSystemAccessorAccessLog(mountName))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = fs.Impl.Unmount(mountName);
            Tick end = fs.Hos.Os.GetSystemTick();

            var sb = new U8StringBuilder(logBuffer, true);
            sb.Append(LogName).Append(mountName).Append((byte)'"');

            fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            res = fs.Impl.Unmount(mountName);
        }

        fs.Impl.LogResultErrorMessage(res);
        Abort.DoAbortUnless(res.IsSuccess());
    }

    public static bool IsMounted(this FileSystemClient fs, U8Span mountName)
    {
        Result res;
        bool isMounted;
        Span<byte> logBuffer = stackalloc byte[0x30];

        if (fs.Impl.IsEnabledAccessLog() && fs.Impl.IsEnabledFileSystemAccessorAccessLog(mountName))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = fs.Impl.IsMounted(out isMounted, mountName);
            Tick end = fs.Hos.Os.GetSystemTick();

            var sb = new U8StringBuilder(logBuffer, true);
            ReadOnlySpan<byte> boolString = AccessLogImpl.ConvertFromBoolToAccessLogBooleanValue(isMounted);
            sb.Append(LogName).Append(mountName).Append(LogIsMounted).Append(boolString).Append((byte)'"');

            fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            res = fs.Impl.IsMounted(out isMounted, mountName);
        }

        fs.Impl.LogResultErrorMessage(res);
        Abort.DoAbortUnless(res.IsSuccess());

        return isMounted;
    }

    public static Result ConvertToFsCommonPath(this FileSystemClient fs, U8SpanMutable commonPathBuffer,
        U8Span path)
    {
        Result res;

        if (commonPathBuffer.IsNull())
        {
            res = ResultFs.NullptrArgument.Value;
            fs.Impl.AbortIfNeeded(res);
            return res;
        }

        if (path.IsNull())
        {
            res = ResultFs.NullptrArgument.Value;
            fs.Impl.AbortIfNeeded(res);
            return res;
        }

        res = GetMountNameAndSubPath(out MountName mountName, out U8Span subPath, path);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = fs.Impl.Find(out FileSystemAccessor fileSystem, new U8Span(mountName.Name));
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = fileSystem.GetCommonMountName(commonPathBuffer.Value);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        int mountNameLength = StringUtils.GetLength(commonPathBuffer);
        int commonPathLength = StringUtils.GetLength(subPath);

        if (mountNameLength + commonPathLength > commonPathBuffer.Length)
            return ResultFs.TooLongPath.Log();

        StringUtils.Copy(commonPathBuffer.Slice(commonPathLength), subPath);
        return Result.Success;
    }
}