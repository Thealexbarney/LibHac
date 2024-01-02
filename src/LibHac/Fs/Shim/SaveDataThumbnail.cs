// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Common;
using LibHac.Fs.Fsa;

namespace LibHac.Fs.Shim;

file static class Anonymous
{
    public static Result ReadMeta(Span<byte> outBuffer, IFile file)
    {
        throw new NotImplementedException();
    }

    public static Result ReadAndCheckHash(Span<byte> outDataBuffer, IFile file, ReadOnlySpan<byte> expectedHash,
        ulong offset, ulong size)
    {
        throw new NotImplementedException();
    }

    public static Result WriteAndCalcHash(Span<byte> outHashBuffer, IFile file, ReadOnlySpan<byte> data, ulong offset,
        ulong size)
    {
        throw new NotImplementedException();
    }

    public static Result OpenSaveDataThumbnailFileImpl(ref UniqueRef<IFile> outFile, SaveDataType saveType,
        ulong programId, UserId userId)
    {
        throw new NotImplementedException();
    }

    public static Result OpenSaveDataThumbnailFileImpl(ref UniqueRef<IFile> outFile, ulong programId, UserId userId)
    {
        throw new NotImplementedException();
    }
}

public static class SaveDataThumbnail
{
    public static Result ReadSaveDataThumbnailFile(this FileSystemClientImpl fs, ulong applicationId, UserId userId,
        Span<byte> headerBuffer, Span<byte> imageDataBuffer)
    {
        throw new NotImplementedException();
    }

    public static Result ReadSaveDataThumbnailFile(this FileSystemClient fs, ulong applicationId, UserId userId,
        Span<byte> headerBuffer, Span<byte> imageDataBuffer)
    {
        throw new NotImplementedException();
    }

    public static Result ReadSaveDataThumbnailFileHeader(this FileSystemClientImpl fs, ulong applicationId,
        UserId userId, Span<byte> headerBuffer)
    {
        throw new NotImplementedException();
    }

    public static Result OpenSaveDataThumbnailFile(this FileSystemClient fs, ref UniqueRef<IFile> outFile,
        ulong applicationId, UserId userId)
    {
        throw new NotImplementedException();
    }

    public static Result OpenSaveDataThumbnailFile(this FileSystemClient fs, ref UniqueRef<IFile> outFile,
        SaveDataType saveType, ulong applicationId, UserId userId)
    {
        throw new NotImplementedException();
    }

    public static Result ReadSaveDataThumbnailFileHeader(this FileSystemClient fs, ulong applicationId,
        UserId userId, Span<byte> headerBuffer)
    {
        throw new NotImplementedException();
    }

    public static Result WriteSaveDataThumbnailFile(this FileSystemClientImpl fs, ulong applicationId, UserId userId,
        ReadOnlySpan<byte> headerBuffer, ReadOnlySpan<byte> imageDataBuffer)
    {
        throw new NotImplementedException();
    }

    public static Result WriteSaveDataThumbnailFile(this FileSystemClient fs, ulong applicationId, UserId userId,
        ReadOnlySpan<byte> headerBuffer, ReadOnlySpan<byte> imageDataBuffer)
    {
        throw new NotImplementedException();
    }

    public static Result WriteSaveDataThumbnailFileHeader(this FileSystemClientImpl fs, ulong applicationId,
        UserId userId, ReadOnlySpan<byte> headerBuffer)
    {
        throw new NotImplementedException();
    }

    public static Result WriteSaveDataThumbnailFileHeader(this FileSystemClient fs, ulong applicationId,
        UserId userId, ReadOnlySpan<byte> headerBuffer)
    {
        throw new NotImplementedException();
    }

    public static Result CorruptSaveDataThumbnailFileForDebug(this FileSystemClientImpl fs, ulong applicationId,
        UserId userId)
    {
        throw new NotImplementedException();
    }

    public static Result CorruptSaveDataThumbnailFileForDebug(this FileSystemClient fs, ulong applicationId,
        UserId userId)
    {
        throw new NotImplementedException();
    }
}