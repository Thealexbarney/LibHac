using System;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.Creators
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
