using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Impl;
using LibHac.Fs.Shim;
using LibHac.Os;
using LibHac.Util;
using static LibHac.Fs.Impl.AccessLogStrings;
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
    /// <param name="subPath">If the method returns successfully, contains the provided path without the
    /// mount name; otherwise the contents are undefined.</param>
    /// <param name="path">The <see cref="Path"/> to process.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.InvalidPathFormat"/>: <paramref name="path"/> does not contain a sub path after
    /// the mount name that begins with <c>/</c> or <c>\</c>.<br/>
    /// <see cref="ResultFs.InvalidMountName"/>: <paramref name="path"/> contains an invalid mount name
    /// or does not have a mount name.</returns>
    private static Result GetMountNameAndSubPath(out MountName mountName, out U8Span subPath, U8Span path)
    {
        UnsafeHelpers.SkipParamInit(out mountName);
        subPath = default;

        int mountLen = 0;
        int maxMountLen = Math.Min(path.Length, PathTool.MountNameLengthMax);

        if (WindowsPath.IsWindowsDrive(path) || WindowsPath.IsUncPath(path))
        {
            StringUtils.Copy(mountName.Name, CommonPaths.HostRootFileSystemMountName);
            mountName.Name[PathTool.MountNameLengthMax] = NullTerminator;

            subPath = path;
            return Result.Success;
        }

        for (int i = 0; i <= maxMountLen; i++)
        {
            if (path[i] == DriveSeparator)
            {
                mountLen = i;
                break;
            }
        }

        if (mountLen == 0)
            return ResultFs.InvalidMountName.Log();

        if (mountLen > maxMountLen)
            return ResultFs.InvalidMountName.Log();

        if (mountLen <= 0)
            return ResultFs.InvalidMountName.Log();

        U8Span subPathTemp = path.Slice(mountLen + 1);

        if (subPathTemp.Length == 0 ||
            (subPathTemp[0] != DirectorySeparator && subPathTemp[0] != AltDirectorySeparator))
            return ResultFs.InvalidPathFormat.Log();

        path.Value.Slice(0, mountLen).CopyTo(mountName.Name);
        mountName.Name[mountLen] = NullTerminator;
        subPath = subPathTemp;

        return Result.Success;
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
        return name.Length > 0 && name[0] == CommonPaths.ReservedMountNamePrefixCharacter;
    }

    internal static Result FindFileSystem(this FileSystemClientImpl fs, out FileSystemAccessor fileSystem,
        out U8Span subPath, U8Span path)
    {
        UnsafeHelpers.SkipParamInit(out fileSystem);
        subPath = default;

        if (path.IsNull())
            return ResultFs.NullptrArgument.Log();

        int hostMountNameLen = StringUtils.GetLength(CommonPaths.HostRootFileSystemMountName);
        if (StringUtils.Compare(path, CommonPaths.HostRootFileSystemMountName, hostMountNameLen) == 0)
        {
            return ResultFs.NotMounted.Log();
        }

        Result rc = GetMountNameAndSubPath(out MountName mountName, out subPath, path);
        if (rc.IsFailure()) return rc;

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
        Result rc = fs.Find(out FileSystemAccessor fileSystem, mountName);
        if (rc.IsFailure()) return rc;

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

        Result rc = fs.Find(out _, mountName);
        if (rc.IsFailure())
        {
            if (!ResultFs.NotMounted.Includes(rc))
                return rc;

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
        Result rc;
        Span<byte> logBuffer = stackalloc byte[0x30];

        if (fs.Impl.IsEnabledAccessLog() && fs.Impl.IsEnabledFileSystemAccessorAccessLog(mountName))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            rc = fs.Impl.Unmount(mountName);
            Tick end = fs.Hos.Os.GetSystemTick();

            var sb = new U8StringBuilder(logBuffer, true);
            sb.Append(LogName).Append(mountName).Append((byte)'"');

            fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            rc = fs.Impl.Unmount(mountName);
        }
        fs.Impl.LogResultErrorMessage(rc);
        Abort.DoAbortUnless(rc.IsSuccess());
    }

    public static bool IsMounted(this FileSystemClient fs, U8Span mountName)
    {
        Result rc;
        bool isMounted;
        Span<byte> logBuffer = stackalloc byte[0x30];

        if (fs.Impl.IsEnabledAccessLog() && fs.Impl.IsEnabledFileSystemAccessorAccessLog(mountName))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            rc = fs.Impl.IsMounted(out isMounted, mountName);
            Tick end = fs.Hos.Os.GetSystemTick();

            var sb = new U8StringBuilder(logBuffer, true);
            ReadOnlySpan<byte> boolString = AccessLogImpl.ConvertFromBoolToAccessLogBooleanValue(isMounted);
            sb.Append(LogName).Append(mountName).Append(LogIsMounted).Append(boolString).Append((byte)'"');

            fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            rc = fs.Impl.IsMounted(out isMounted, mountName);
        }
        fs.Impl.LogResultErrorMessage(rc);
        Abort.DoAbortUnless(rc.IsSuccess());

        return isMounted;
    }

    public static Result ConvertToFsCommonPath(this FileSystemClient fs, U8SpanMutable commonPathBuffer,
        U8Span path)
    {
        Result rc;

        if (commonPathBuffer.IsNull())
        {
            rc = ResultFs.NullptrArgument.Value;
            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        if (path.IsNull())
        {
            rc = ResultFs.NullptrArgument.Value;
            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        rc = GetMountNameAndSubPath(out MountName mountName, out U8Span subPath, path);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc;

        rc = fs.Impl.Find(out FileSystemAccessor fileSystem, new U8Span(mountName.Name));
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc;

        rc = fileSystem.GetCommonMountName(commonPathBuffer.Value);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc;

        int mountNameLength = StringUtils.GetLength(commonPathBuffer);
        int commonPathLength = StringUtils.GetLength(subPath);

        if (mountNameLength + commonPathLength > commonPathBuffer.Length)
            return ResultFs.TooLongPath.Log();

        StringUtils.Copy(commonPathBuffer.Slice(commonPathLength), subPath);
        return Result.Success;
    }
}