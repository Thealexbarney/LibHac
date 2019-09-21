using System;
using LibHac.Fs;

namespace LibHac.FsService.Creators
{
    public interface IEncryptedFileSystemCreator
    {
        Result Create(out IFileSystem encryptedFileSystem, IFileSystem baseFileSystem, EncryptedFsKeyId keyId,
            ReadOnlySpan<byte> encryptionSeed);
    }

    public enum EncryptedFsKeyId
    {
        Save = 0,
        Content = 1,
        CustomStorage = 2
    }
}
