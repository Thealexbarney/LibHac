using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using static LibHac.FsSrv.FsCreator.IEncryptedFileSystemCreator;

namespace LibHac.FsSrv.FsCreator
{
    public class EncryptedFileSystemCreator : IEncryptedFileSystemCreator
    {
        private KeySet KeySet { get; }

        public EncryptedFileSystemCreator(KeySet keySet)
        {
            KeySet = keySet;
        }

        public Result Create(out ReferenceCountedDisposable<IFileSystem> encryptedFileSystem,
            ref ReferenceCountedDisposable<IFileSystem> baseFileSystem, KeyId idIndex,
            in EncryptionSeed encryptionSeed)
        {
            UnsafeHelpers.SkipParamInit(out encryptedFileSystem);

            if (idIndex < KeyId.Save || idIndex > KeyId.CustomStorage)
            {
                return ResultFs.InvalidArgument.Log();
            }

            // todo: "proper" key generation instead of a lazy hack
            KeySet.SetSdSeed(encryptionSeed.Value);

            // Todo: pass ReferenceCountedDisposable to AesXtsFileSystem
            var fs = new AesXtsFileSystem(baseFileSystem, KeySet.SdCardEncryptionKeys[(int)idIndex].DataRo.ToArray(),
                0x4000);
            encryptedFileSystem = new ReferenceCountedDisposable<IFileSystem>(fs);

            return Result.Success;
        }
    }
}
