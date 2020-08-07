using System;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;

namespace LibHac.FsSrv.Creators
{
    public class EncryptedFileSystemCreator : IEncryptedFileSystemCreator
    {
        private Keyset Keyset { get; }

        public EncryptedFileSystemCreator(Keyset keyset)
        {
            Keyset = keyset;
        }

        public Result Create(out IFileSystem encryptedFileSystem, IFileSystem baseFileSystem, EncryptedFsKeyId keyId,
            ReadOnlySpan<byte> encryptionSeed)
        {
            encryptedFileSystem = default;

            if (keyId < EncryptedFsKeyId.Save || keyId > EncryptedFsKeyId.CustomStorage)
            {
                return ResultFs.InvalidArgument.Log();
            }

            // todo: "proper" key generation instead of a lazy hack
            Keyset.SetSdSeed(encryptionSeed.ToArray());

            encryptedFileSystem = new AesXtsFileSystem(baseFileSystem, Keyset.SdCardKeys[(int)keyId], 0x4000);

            return Result.Success;
        }
    }
}
