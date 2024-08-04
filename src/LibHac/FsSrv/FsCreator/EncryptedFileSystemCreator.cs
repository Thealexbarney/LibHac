#pragma warning disable CS0169 // Field is never used
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.FsSystem;
using static LibHac.FsSrv.FsCreator.IEncryptedFileSystemCreator;

namespace LibHac.FsSrv.FsCreator;

public class EncryptedFileSystemCreator : IEncryptedFileSystemCreator
{
    private readonly KeySet _keySet;
    private Configuration _configuration;
    private EncryptionSeed _seed;

    public struct Configuration
    {
        public RandomDataGenerator GenerateRandomData;
        public GenerateSdEncryptionKey GenerateSdEncryptionKey;
    }

    public EncryptedFileSystemCreator(KeySet keySet)
    {
        _keySet = keySet;
    }

    private ref readonly EncryptionSeed GetSeed(KeyId keyId)
    {
        switch (keyId)
        {
            case KeyId.Save:
            case KeyId.Content:
            case KeyId.CustomStorage:
                return ref _seed;
            default:
                Abort.UnexpectedDefault();
                return ref Unsafe.NullRef<EncryptionSeed>();
        }
    }

    public Result Create(ref SharedRef<IFileSystem> outEncryptedFileSystem, ref readonly SharedRef<IFileSystem> baseFileSystem, KeyId keyId)
    {
        if (keyId < KeyId.Save || keyId > KeyId.CustomStorage)
        {
            return ResultFs.InvalidArgument.Log();
        }

        // todo: "proper" key generation instead of a lazy hack
        _keySet.SetSdSeed(GetSeed(keyId).Value);

        using var encryptedFileSystem = new SharedRef<AesXtsFileSystem>(new AesXtsFileSystem(in baseFileSystem,
            _keySet.SdCardEncryptionKeys[(int)keyId].DataRo.ToArray(), 0x4000));

        outEncryptedFileSystem.SetByMove(ref encryptedFileSystem.Ref);

        return Result.Success;
    }

    public Result SetEncryptionSeed(KeyId keyId, in EncryptionSeed encryptionSeed)
    {
        switch (keyId)
        {
            case KeyId.Save:
            case KeyId.Content:
            case KeyId.CustomStorage:
                _seed = encryptionSeed;
                break;
            default:
                Abort.UnexpectedDefault();
                break;
        }

        return Result.Success;
    }
}