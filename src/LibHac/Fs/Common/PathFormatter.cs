using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Util;
using static LibHac.Fs.StringTraits;

// ReSharper disable once CheckNamespace
namespace LibHac.Fs;

/// <summary>
/// Contains functions for working with path formatting and normalization.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
public static class PathFormatter
{
    private static ReadOnlySpan<byte> InvalidCharacter =>
        new[] { (byte)':', (byte)'*', (byte)'?', (byte)'<', (byte)'>', (byte)'|' };

    private static ReadOnlySpan<byte> InvalidCharacterForHostName =>
        new[] { (byte)':', (byte)'*', (byte)'<', (byte)'>', (byte)'|', (byte)'$' };

    private static ReadOnlySpan<byte> InvalidCharacterForMountName =>
        new[] { (byte)'*', (byte)'?', (byte)'<', (byte)'>', (byte)'|' };


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Result CheckHostName(ReadOnlySpan<byte> name)
    {
        if (name.Length == 2 && name[0] == Dot && name[1] == Dot)
            return ResultFs.InvalidPathFormat.Log();

        for (int i = 0; i < name.Length; i++)
        {
            foreach (byte c in InvalidCharacterForHostName)
            {
                if (name[i] == c)
                    return ResultFs.InvalidCharacter.Log();
            }
        }

        return Result.Success;
    }

    private static Result CheckSharedName(ReadOnlySpan<byte> name)
    {
        if (name.Length == 1 && name[0] == Dot)
            return ResultFs.InvalidPathFormat.Log();

        if (name.Length == 2 && name[0] == Dot && name[1] == Dot)
            return ResultFs.InvalidPathFormat.Log();

        for (int i = 0; i < name.Length; i++)
        {
            foreach (byte c in InvalidCharacter)
            {
                if (name[i] == c)
                    return ResultFs.InvalidCharacter.Log();
            }
        }

        return Result.Success;
    }

    public static Result ParseMountName(out ReadOnlySpan<byte> newPath, out int mountNameLength,
        Span<byte> outMountNameBuffer, ReadOnlySpan<byte> path)
    {
        Assert.SdkRequiresNotNull(path);

        UnsafeHelpers.SkipParamInit(out mountNameLength);
        newPath = default;

        int maxMountLength = outMountNameBuffer.Length == 0
            ? PathTool.MountNameLengthMax + 1
            : Math.Min(outMountNameBuffer.Length, PathTool.MountNameLengthMax + 1);

        int mountLength;
        for (mountLength = 0; mountLength < maxMountLength && path.At(mountLength) != 0; mountLength++)
        {
            byte c = path[mountLength];

            if (c == DriveSeparator)
            {
                mountLength++;
                break;
            }

            if (c == DirectorySeparator || c == AltDirectorySeparator)
            {
                newPath = path;
                mountNameLength = 0;

                return Result.Success;
            }
        }

        if (mountLength <= 2 || path[mountLength - 1] != DriveSeparator)
        {
            newPath = path;
            mountNameLength = 0;

            return Result.Success;
        }

        for (int i = 0; i < mountLength; i++)
        {
            foreach (byte c in InvalidCharacterForMountName)
            {
                if (path.At(i) == c)
                    return ResultFs.InvalidCharacter.Log();
            }
        }

        if (!outMountNameBuffer.IsEmpty)
        {
            if (mountLength >= outMountNameBuffer.Length)
                return ResultFs.TooLongPath.Log();

            path.Slice(0, mountLength).CopyTo(outMountNameBuffer);
            outMountNameBuffer[mountLength] = NullTerminator;
        }

        newPath = path.Slice(mountLength);
        mountNameLength = mountLength;
        return Result.Success;
    }

    public static Result SkipMountName(out ReadOnlySpan<byte> newPath, out int mountNameLength,
        ReadOnlySpan<byte> path)
    {
        return ParseMountName(out newPath, out mountNameLength, Span<byte>.Empty, path);
    }

    private static Result ParseWindowsPathImpl(out ReadOnlySpan<byte> newPath, out int windowsPathLength,
        Span<byte> normalizeBuffer, ReadOnlySpan<byte> path, bool hasMountName)
    {
        Assert.SdkRequiresNotNull(path);

        UnsafeHelpers.SkipParamInit(out windowsPathLength);
        newPath = default;

        if (normalizeBuffer.Length != 0)
            normalizeBuffer[0] = NullTerminator;

        ReadOnlySpan<byte> currentPath = path;

        if (hasMountName && path.At(0) == DirectorySeparator)
        {
            if (path.At(1) == AltDirectorySeparator && path.At(2) == AltDirectorySeparator)
            {
                if (normalizeBuffer.Length == 0)
                    return ResultFs.NotNormalized.Log();

                currentPath = path.Slice(1);
            }
            else if (WindowsPath.IsWindowsDrive(path.Slice(1)))
            {
                if (normalizeBuffer.Length == 0)
                    return ResultFs.NotNormalized.Log();

                currentPath = path.Slice(1);
            }
        }

        if (WindowsPath.IsWindowsDrive(currentPath))
        {
            int winPathLength;
            for (winPathLength = 2; currentPath.At(winPathLength) != NullTerminator; winPathLength++)
            {
                foreach (byte c in InvalidCharacter)
                {
                    if (currentPath[winPathLength] == c)
                        return ResultFs.InvalidCharacter.Log();
                }

                if (currentPath[winPathLength] == DirectorySeparator ||
                    currentPath[winPathLength] == AltDirectorySeparator)
                {
                    break;
                }
            }

            if (normalizeBuffer.IsEmpty)
            {
                for (int i = 0; i < winPathLength; i++)
                {
                    if (currentPath[i] == '\\')
                        return ResultFs.NotNormalized.Log();
                }
            }

            if (!normalizeBuffer.IsEmpty)
            {
                if (winPathLength >= normalizeBuffer.Length)
                    return ResultFs.TooLongPath.Log();

                currentPath.Slice(0, winPathLength).CopyTo(normalizeBuffer);
                normalizeBuffer[winPathLength] = NullTerminator;
                PathUtility.Replace(normalizeBuffer.Slice(0, winPathLength), AltDirectorySeparator,
                    DirectorySeparator);
            }

            newPath = currentPath.Slice(winPathLength);
            windowsPathLength = winPathLength;
            return Result.Success;
        }

        if (WindowsPath.IsDosDevicePath(currentPath))
        {
            int dosPathLength = WindowsPath.GetDosDevicePathPrefixLength();

            if (WindowsPath.IsWindowsDrive(currentPath.Slice(dosPathLength)))
            {
                dosPathLength += 2;
            }
            else
            {
                dosPathLength--;
            }

            if (!normalizeBuffer.IsEmpty)
            {
                if (dosPathLength >= normalizeBuffer.Length)
                    return ResultFs.TooLongPath.Log();

                currentPath.Slice(0, dosPathLength).CopyTo(normalizeBuffer);
                normalizeBuffer[dosPathLength] = NullTerminator;
                PathUtility.Replace(normalizeBuffer.Slice(0, dosPathLength), DirectorySeparator,
                    AltDirectorySeparator);
            }

            newPath = currentPath.Slice(dosPathLength);
            windowsPathLength = dosPathLength;
            return Result.Success;
        }

        if (WindowsPath.IsUncPath(currentPath, false, true))
        {
            Result rc;

            ReadOnlySpan<byte> finalPath = currentPath;

            if (currentPath.At(2) == DirectorySeparator || currentPath.At(2) == AltDirectorySeparator)
                return ResultFs.InvalidPathFormat.Log();

            int currentComponentOffset = 0;
            int pos;
            for (pos = 2; currentPath.At(pos) != NullTerminator; pos++)
            {
                if (currentPath.At(pos) == DirectorySeparator || currentPath.At(pos) == AltDirectorySeparator)
                {
                    if (currentComponentOffset != 0)
                    {
                        rc = CheckSharedName(
                            currentPath.Slice(currentComponentOffset, pos - currentComponentOffset));
                        if (rc.IsFailure()) return rc;

                        finalPath = currentPath.Slice(pos);
                        break;
                    }

                    if (currentPath.At(pos + 1) == DirectorySeparator || currentPath.At(pos + 1) == AltDirectorySeparator)
                        return ResultFs.InvalidPathFormat.Log();

                    rc = CheckHostName(currentPath.Slice(2, pos - 2));
                    if (rc.IsFailure()) return rc;

                    currentComponentOffset = pos + 1;
                }
            }

            if (currentComponentOffset == pos)
                return ResultFs.InvalidPathFormat.Log();

            if (currentComponentOffset != 0 && finalPath == currentPath)
            {
                rc = CheckSharedName(currentPath.Slice(currentComponentOffset, pos - currentComponentOffset));
                if (rc.IsFailure()) return rc;

                finalPath = currentPath.Slice(pos);
            }

            ref byte currentPathStart = ref MemoryMarshal.GetReference(currentPath);
            ref byte finalPathStart = ref MemoryMarshal.GetReference(finalPath);
            int uncPrefixLength = (int)Unsafe.ByteOffset(ref currentPathStart, ref finalPathStart);

            if (normalizeBuffer.IsEmpty)
            {
                for (int i = 0; i < uncPrefixLength; i++)
                {
                    if (currentPath[i] == DirectorySeparator)
                        return ResultFs.NotNormalized.Log();
                }
            }

            if (!normalizeBuffer.IsEmpty)
            {
                if (uncPrefixLength >= normalizeBuffer.Length)
                    return ResultFs.TooLongPath.Log();

                currentPath.Slice(0, uncPrefixLength).CopyTo(normalizeBuffer);
                normalizeBuffer[uncPrefixLength] = NullTerminator;
                PathUtility.Replace(normalizeBuffer.Slice(0, uncPrefixLength), DirectorySeparator, AltDirectorySeparator);
            }

            newPath = finalPath;
            windowsPathLength = uncPrefixLength;
            return Result.Success;
        }

        newPath = path;
        windowsPathLength = 0;
        return Result.Success;
    }

    public static Result ParseWindowsPath(out ReadOnlySpan<byte> newPath, out int windowsPathLength,
        Span<byte> normalizeBuffer, ReadOnlySpan<byte> path, bool hasMountName)
    {
        return ParseWindowsPathImpl(out newPath, out windowsPathLength, normalizeBuffer, path, hasMountName);
    }

    public static Result SkipWindowsPath(out ReadOnlySpan<byte> newPath, out int windowsPathLength,
        out bool isNormalized, ReadOnlySpan<byte> path, bool hasMountName)
    {
        isNormalized = true;

        Result rc = ParseWindowsPathImpl(out newPath, out windowsPathLength, Span<byte>.Empty, path, hasMountName);
        if (!rc.IsSuccess())
        {
            if (ResultFs.NotNormalized.Includes(rc))
            {
                isNormalized = false;
            }
            else
            {
                return rc;
            }
        }

        return Result.Success;
    }

    private static Result ParseRelativeDotPathImpl(out ReadOnlySpan<byte> newPath, out int length,
        Span<byte> relativePathBuffer, ReadOnlySpan<byte> path)
    {
        Assert.SdkRequiresNotNull(path);

        UnsafeHelpers.SkipParamInit(out length);
        newPath = default;

        if (relativePathBuffer.Length != 0)
            relativePathBuffer[0] = NullTerminator;

        if (path.At(0) == Dot && (path.At(1) == NullTerminator || path.At(1) == DirectorySeparator ||
                                  path.At(1) == AltDirectorySeparator))
        {
            if (relativePathBuffer.Length != 0)
            {
                // Note: Nintendo doesn't check if the buffer is long enough here
                if (relativePathBuffer.Length < 2)
                    return ResultFs.TooLongPath.Log();

                relativePathBuffer[0] = Dot;
                relativePathBuffer[1] = NullTerminator;
            }

            newPath = path.Slice(1);
            length = 1;
            return Result.Success;
        }

        if (path.At(0) == Dot && path.At(1) == Dot)
            return ResultFs.DirectoryUnobtainable.Log();

        newPath = path;
        length = 0;
        return Result.Success;
    }

    public static Result ParseRelativeDotPath(out ReadOnlySpan<byte> newPath, out int length,
        Span<byte> relativePathBuffer, ReadOnlySpan<byte> path)
    {
        return ParseRelativeDotPathImpl(out newPath, out length, relativePathBuffer, path);
    }

    public static Result SkipRelativeDotPath(out ReadOnlySpan<byte> newPath, out int length,
        ReadOnlySpan<byte> path)
    {
        return ParseRelativeDotPathImpl(out newPath, out length, Span<byte>.Empty, path);
    }

    public static Result IsNormalized(out bool isNormalized, out int normalizedLength, ReadOnlySpan<byte> path,
        PathFlags flags)
    {
        UnsafeHelpers.SkipParamInit(out isNormalized, out normalizedLength);

        Result rc = PathUtility.CheckUtf8(path);
        if (rc.IsFailure()) return rc;

        ReadOnlySpan<byte> buffer = path;
        int totalLength = 0;

        if (path.At(0) == NullTerminator)
        {
            if (!flags.IsEmptyPathAllowed())
                return ResultFs.InvalidPathFormat.Log();

            isNormalized = true;
            normalizedLength = 0;
            return Result.Success;
        }

        if (path.At(0) != DirectorySeparator &&
            !flags.IsWindowsPathAllowed() &&
            !flags.IsRelativePathAllowed() &&
            !flags.IsMountNameAllowed())
        {
            return ResultFs.InvalidPathFormat.Log();
        }

        if (WindowsPath.IsWindowsPath(path, false) && !flags.IsWindowsPathAllowed())
            return ResultFs.InvalidPathFormat.Log();

        bool hasMountName = false;

        rc = SkipMountName(out buffer, out int mountNameLength, buffer);
        if (rc.IsFailure()) return rc;

        if (mountNameLength != 0)
        {
            if (!flags.IsMountNameAllowed())
                return ResultFs.InvalidPathFormat.Log();

            totalLength += mountNameLength;
            hasMountName = true;
        }

        if (buffer.At(0) != DirectorySeparator && !PathUtility.IsPathStartWithCurrentDirectory(buffer) &&
            !WindowsPath.IsWindowsPath(buffer, false))
        {
            if (!flags.IsRelativePathAllowed() || !PathUtility.CheckInvalidCharacter(buffer.At(0)).IsSuccess())
                return ResultFs.InvalidPathFormat.Log();

            isNormalized = false;
            return Result.Success;
        }

        bool isRelativePath = false;

        rc = SkipRelativeDotPath(out buffer, out int relativePathLength, buffer);
        if (rc.IsFailure()) return rc;

        if (relativePathLength != 0)
        {
            if (!flags.IsRelativePathAllowed())
                return ResultFs.InvalidPathFormat.Log();

            totalLength += relativePathLength;

            if (buffer.At(0) == NullTerminator)
            {
                isNormalized = true;
                normalizedLength = totalLength;
                return Result.Success;
            }

            isRelativePath = true;
        }

        rc = SkipWindowsPath(out buffer, out int windowsPathLength, out bool isNormalizedWin, buffer, hasMountName);
        if (rc.IsFailure()) return rc;

        if (!isNormalizedWin)
        {
            if (!flags.IsWindowsPathAllowed())
                return ResultFs.InvalidPathFormat.Log();

            isNormalized = false;
            return Result.Success;
        }

        if (windowsPathLength != 0)
        {
            if (!flags.IsWindowsPathAllowed())
                return ResultFs.InvalidPathFormat.Log();

            totalLength += windowsPathLength;

            if (isRelativePath)
                return ResultFs.InvalidPathFormat.Log();

            if (buffer.At(0) == NullTerminator)
            {
                isNormalized = false;
                return Result.Success;
            }

            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i] == AltDirectorySeparator)
                {
                    isNormalized = false;
                    return Result.Success;
                }
            }
        }

        if (flags.IsBackslashAllowed() && PathNormalizer.IsParentDirectoryPathReplacementNeeded(buffer))
        {
            isNormalized = false;
            return Result.Success;
        }

        rc = PathUtility.CheckInvalidBackslash(out bool isBackslashContained, buffer,
            flags.IsWindowsPathAllowed() || flags.IsBackslashAllowed());
        if (rc.IsFailure()) return rc;

        if (isBackslashContained && !flags.IsBackslashAllowed())
        {
            isNormalized = false;
            return Result.Success;
        }

        rc = PathNormalizer.IsNormalized(out isNormalized, out int length, buffer, flags.AreAllCharactersAllowed());
        if (rc.IsFailure()) return rc;

        totalLength += length;
        normalizedLength = totalLength;
        return Result.Success;
    }

    public static Result Normalize(Span<byte> outputBuffer, ReadOnlySpan<byte> path, PathFlags flags)
    {
        Result rc;

        ReadOnlySpan<byte> src = path;
        int currentPos = 0;
        bool isWindowsPath = false;

        if (path.At(0) == NullTerminator)
        {
            if (!flags.IsEmptyPathAllowed())
                return ResultFs.InvalidPathFormat.Log();

            if (outputBuffer.Length != 0)
                outputBuffer[0] = NullTerminator;

            return Result.Success;
        }

        bool hasMountName = false;

        if (flags.IsMountNameAllowed())
        {
            rc = ParseMountName(out src, out int mountNameLength, outputBuffer.Slice(currentPos), src);
            if (rc.IsFailure()) return rc;

            currentPos += mountNameLength;
            hasMountName = mountNameLength != 0;
        }

        bool isDriveRelative = false;

        if (src.At(0) != DirectorySeparator && !PathUtility.IsPathStartWithCurrentDirectory(src) &&
            !WindowsPath.IsWindowsPath(src, false))
        {
            if (!flags.IsRelativePathAllowed() || !PathUtility.CheckInvalidCharacter(src.At(0)).IsSuccess())
                return ResultFs.InvalidPathFormat.Log();

            outputBuffer[currentPos++] = Dot;
            isDriveRelative = true;
        }

        if (flags.IsRelativePathAllowed())
        {
            if (currentPos >= outputBuffer.Length)
                return ResultFs.TooLongPath.Log();

            rc = ParseRelativeDotPath(out src, out int relativePathLength, outputBuffer.Slice(currentPos), src);
            if (rc.IsFailure()) return rc;

            currentPos += relativePathLength;

            if (src.At(0) == NullTerminator)
            {
                if (currentPos >= outputBuffer.Length)
                    return ResultFs.TooLongPath.Log();

                outputBuffer[currentPos] = NullTerminator;
                return Result.Success;
            }
        }

        if (flags.IsWindowsPathAllowed())
        {
            ReadOnlySpan<byte> originalPath = src;

            if (currentPos >= outputBuffer.Length)
                return ResultFs.TooLongPath.Log();

            rc = ParseWindowsPath(out src, out int windowsPathLength, outputBuffer.Slice(currentPos), src,
                hasMountName);
            if (rc.IsFailure()) return rc;

            currentPos += windowsPathLength;

            if (src.At(0) == NullTerminator)
            {
                // Note: Bug is in the original code. Should be "currentPos + 2"
                if (currentPos + 1 >= outputBuffer.Length)
                    return ResultFs.TooLongPath.Log();

                outputBuffer[currentPos] = DirectorySeparator;
                outputBuffer[currentPos + 1] = NullTerminator;
                return Result.Success;
            }

            int skippedLength = (int)Unsafe.ByteOffset(ref MemoryMarshal.GetReference(originalPath),
                ref MemoryMarshal.GetReference(src));

            if (skippedLength > 0)
                isWindowsPath = true;
        }

        rc = PathUtility.CheckInvalidBackslash(out bool isBackslashContained, src,
            flags.IsWindowsPathAllowed() || flags.IsBackslashAllowed());
        if (rc.IsFailure()) return rc;

        byte[] srcBufferSlashReplaced = null;
        try
        {
            if (isBackslashContained && flags.IsWindowsPathAllowed())
            {
                srcBufferSlashReplaced = ArrayPool<byte>.Shared.Rent(path.Length);

                StringUtils.Copy(srcBufferSlashReplaced, path);
                PathUtility.Replace(srcBufferSlashReplaced, AltDirectorySeparator, DirectorySeparator);

                int srcOffset = (int)Unsafe.ByteOffset(ref MemoryMarshal.GetReference(path),
                    ref MemoryMarshal.GetReference(src));

                src = srcBufferSlashReplaced.AsSpan(srcOffset);
            }

            rc = PathNormalizer.Normalize(outputBuffer.Slice(currentPos), out _, src, isWindowsPath, isDriveRelative,
                flags.AreAllCharactersAllowed());
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }
        finally
        {
            if (srcBufferSlashReplaced is not null)
            {
                ArrayPool<byte>.Shared.Return(srcBufferSlashReplaced);
            }
        }
    }

    public static Result CheckPathFormat(ReadOnlySpan<byte> path, PathFlags flags)
    {
        return Result.Success;
    }
}