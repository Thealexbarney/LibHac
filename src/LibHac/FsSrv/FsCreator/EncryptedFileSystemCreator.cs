using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Tools.FsSystem;
using static LibHac.FsSrv.FsCreator.IEncryptedFileSystemCreator;

namespace LibHac.FsSrv.FsCreator;

public class EncryptedFileSystemCreator : IEncryptedFileSystemCreator
{
    private KeySet KeySet { get; }

    public EncryptedFileSystemCreator(KeySet keySet)
    {
        KeySet = keySet;
    }

    public Result Create(ref SharedRef<IFileSystem> outEncryptedFileSystem, ref readonly SharedRef<IFileSystem> baseFileSystem, KeyId idIndex, in EncryptionSeed encryptionSeed)
    {
        if (idIndex < KeyId.Save || idIndex > KeyId.CustomStorage)
        {
            return ResultFs.InvalidArgument.Log();
        }

        // todo: "proper" key generation instead of a lazy hack
        KeySet.SetSdSeed(encryptionSeed.Value);

        using var encryptedFileSystem = new SharedRef<AesXtsFileSystem>(new AesXtsFileSystem(in baseFileSystem,
            KeySet.SdCardEncryptionKeys[(int)idIndex].DataRo.ToArray(), 0x4000));

        outEncryptedFileSystem.SetByMove(ref encryptedFileSystem.Ref);

        return Result.Success;
    }
}