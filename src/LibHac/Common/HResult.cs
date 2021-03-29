using System.Diagnostics.CodeAnalysis;
using LibHac.Fs;

namespace LibHac.Common
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal static class HResult
    {
        public const int ERROR_FILE_NOT_FOUND = unchecked((int)0x80070002);
        public const int ERROR_PATH_NOT_FOUND = unchecked((int)0x80070003);
        public const int ERROR_ACCESS_DENIED = unchecked((int)0x80070005);
        public const int ERROR_SHARING_VIOLATION = unchecked((int)0x80070020);
        public const int ERROR_HANDLE_EOF = unchecked((int)0x80070026);
        public const int ERROR_HANDLE_DISK_FULL = unchecked((int)0x80070027);
        public const int ERROR_FILE_EXISTS = unchecked((int)0x80070050);
        public const int ERROR_DISK_FULL = unchecked((int)0x80070070);
        public const int ERROR_INVALID_NAME = unchecked((int)0x8007007B);
        public const int ERROR_DIR_NOT_EMPTY = unchecked((int)0x80070091);
        public const int ERROR_ALREADY_EXISTS = unchecked((int)0x800700B7);
        public const int ERROR_DIRECTORY = unchecked((int)0x8007010B);
        public const int ERROR_SPACES_NOT_ENOUGH_DRIVES = unchecked((int)0x80E7000B);

        public static Result HResultToHorizonResult(int hResult) => hResult switch
        {
            ERROR_FILE_NOT_FOUND => ResultFs.PathNotFound.Value,
            ERROR_PATH_NOT_FOUND => ResultFs.PathNotFound.Value,
            ERROR_ACCESS_DENIED => ResultFs.TargetLocked.Value,
            ERROR_SHARING_VIOLATION => ResultFs.TargetLocked.Value,
            ERROR_HANDLE_EOF => ResultFs.OutOfRange.Value,
            ERROR_HANDLE_DISK_FULL => ResultFs.UsableSpaceNotEnough.Value,
            ERROR_FILE_EXISTS => ResultFs.PathAlreadyExists.Value,
            ERROR_DISK_FULL => ResultFs.UsableSpaceNotEnough.Value,
            ERROR_INVALID_NAME => ResultFs.PathNotFound.Value,
            ERROR_DIR_NOT_EMPTY => ResultFs.DirectoryNotEmpty.Value,
            ERROR_ALREADY_EXISTS => ResultFs.PathAlreadyExists.Value,
            ERROR_DIRECTORY => ResultFs.PathNotFound.Value,
            ERROR_SPACES_NOT_ENOUGH_DRIVES => ResultFs.UsableSpaceNotEnough.Value,
            _ => ResultFs.UnexpectedInLocalFileSystemE.Value
        };
    }
}
