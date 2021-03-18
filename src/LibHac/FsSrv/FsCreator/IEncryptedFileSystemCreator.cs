using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.FsCreator
{
    public interface IEncryptedFileSystemCreator
    {
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
