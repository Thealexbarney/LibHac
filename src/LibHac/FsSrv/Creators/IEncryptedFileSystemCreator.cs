using System;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.Creators
{
    public interface IEncryptedFileSystemCreator
    {
        // Todo: remove the function using raw IFileSystems
        Result Create(out IFileSystem encryptedFileSystem, IFileSystem baseFileSystem, EncryptedFsKeyId keyId,
            ReadOnlySpan<byte> encryptionSeed);

        Result Create(out ReferenceCountedDisposable<IFileSystem> encryptedFileSystem,
            ReferenceCountedDisposable<IFileSystem> baseFileSystem, EncryptedFsKeyId keyId,
            in EncryptionSeed encryptionSeed);
    }

    public enum EncryptedFsKeyId
    {
        Save = 0,
        Content = 1,
        CustomStorage = 2
    }
}
