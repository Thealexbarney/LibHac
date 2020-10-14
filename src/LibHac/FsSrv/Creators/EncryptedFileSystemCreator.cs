using System;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;

namespace LibHac.FsSrv.Creators
{
    public class EncryptedFileSystemCreator : IEncryptedFileSystemCreator
    {
        private KeySet KeySet { get; }

        public EncryptedFileSystemCreator(KeySet keySet)
        {
            KeySet = keySet;
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
            KeySet.SetSdSeed(encryptionSeed.ToArray());

            encryptedFileSystem = new AesXtsFileSystem(baseFileSystem,
                KeySet.SdCardEncryptionKeys[(int) keyId].DataRo.ToArray(), 0x4000);

            return Result.Success;
        }
    }
}
