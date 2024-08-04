using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.FsCreator;

public interface IEncryptedFileSystemCreator
{
    public enum KeyId
    {
        Save = 0,
        Content = 1,
        CustomStorage = 2
    }

    Result Create(ref SharedRef<IFileSystem> outEncryptedFileSystem, ref readonly SharedRef<IFileSystem> baseFileSystem,
        KeyId keyId);

    Result SetEncryptionSeed(KeyId keyId, in EncryptionSeed encryptionSeed);
}