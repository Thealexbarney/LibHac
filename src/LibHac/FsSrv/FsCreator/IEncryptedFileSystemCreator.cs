using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.FsCreator
{
    public interface IEncryptedFileSystemCreator
    {
        public enum KeyId
        {
            Save = 0,
            Content = 1,
            CustomStorage = 2
        }

        Result Create(out ReferenceCountedDisposable<IFileSystem> encryptedFileSystem,
            ref ReferenceCountedDisposable<IFileSystem> baseFileSystem, KeyId idIndex,
            in EncryptionSeed encryptionSeed);
    }

    public enum EncryptedFsKeyId
    {
        Save = 0,
        Content = 1,
        CustomStorage = 2
    }
}
