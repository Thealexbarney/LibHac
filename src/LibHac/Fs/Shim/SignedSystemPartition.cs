using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;

namespace LibHac.Fs.Shim;

/// <summary>
/// Contains functions for checking if a mounted file system was created from a signed system partition.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0</remarks>
public static class SignedSystemPartition
{
    public static bool IsValidSignedSystemPartitionOnSdCard(this FileSystemClient fs, U8Span path)
    {
        Result rc = fs.Impl.FindFileSystem(out FileSystemAccessor fileSystem, out U8Span _, path);
        fs.Impl.LogResultErrorMessage(rc);
        Abort.DoAbortUnlessSuccess(rc);

        bool isValid = false;

        rc = Operate(ref isValid, fileSystem);
        fs.Impl.LogResultErrorMessage(rc);
        Abort.DoAbortUnlessSuccess(rc);

        return isValid;

        static Result Operate(ref bool isValid, FileSystemAccessor fileSystem)
        {
            Result rc = fileSystem.QueryEntry(SpanHelpers.AsByteSpan(ref isValid), ReadOnlySpan<byte>.Empty,
                QueryId.IsSignedSystemPartition, new U8Span(new[] { (byte)'/' }));

            if (rc.IsFailure())
            {
                // Any IFileSystems other than a signed system partition IFileSystem should
                // return an "UnsupportedOperation" result
                if (ResultFs.UnsupportedOperation.Includes(rc))
                {
                    rc.Catch();
                    isValid = false;
                }
                else
                {
                    return rc.Miss();
                }
            }

            return Result.Success;
        }
    }
}