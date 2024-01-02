using System;
using LibHac.Account;

namespace LibHac.Fs;

public static class SaveDataThumbnail
{
    public static Result ReadSaveDataThumbnailFile(this FileSystemClientImpl fs, ulong applicationId, in Uid uid,
        Span<byte> headerBuffer, Span<byte> imageDataBuffer)
    {
        throw new NotImplementedException();
    }

    public static Result ReadSaveDataThumbnailFileHeader(this FileSystemClientImpl fs, ulong applicationId, in Uid uid,
        Span<byte> headerBuffer)
    {
        throw new NotImplementedException();
    }

    public static Result WriteSaveDataThumbnailFile(this FileSystemClient fs, ulong applicationId, in Uid uid,
        ReadOnlySpan<byte> headerBuffer, ReadOnlySpan<byte> imageDataBuffer)
    {
        throw new NotImplementedException();
    }

    public static Result WriteSaveDataThumbnailFileHeader(this FileSystemClient fs, ulong applicationId, in Uid uid,
        ReadOnlySpan<byte> headerBuffer)
    {
        throw new NotImplementedException();
    }

    public static Result CorruptSaveDataThumbnailFileForDebug(this FileSystemClient fs, ulong applicationId, in Uid uid)
    {
        throw new NotImplementedException();
    }
}