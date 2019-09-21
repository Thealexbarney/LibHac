using System;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    public class AesXtsFile : FileBase
    {
        private IFile BaseFile { get; }
        private string Path { get; }
        private byte[] KekSeed { get; }
        private byte[] VerificationKey { get; }
        private int BlockSize { get; }

        private AesXtsFileHeader Header { get; }
        private IStorage BaseStorage { get; }

        internal const int HeaderLength = 0x4000;

        public AesXtsFile(OpenMode mode, IFile baseFile, string path, ReadOnlySpan<byte> kekSeed, ReadOnlySpan<byte> verificationKey, int blockSize)
        {
            Mode = mode;
            BaseFile = baseFile;
            Path = path;
            KekSeed = kekSeed.ToArray();
            VerificationKey = verificationKey.ToArray();
            BlockSize = blockSize;

            Header = new AesXtsFileHeader(BaseFile);

            baseFile.GetSize(out long fileSize).ThrowIfFailure();

            if (!Header.TryDecryptHeader(Path, KekSeed, VerificationKey))
            {
                ThrowHelper.ThrowResult(ResultFs.AesXtsFileHeaderInvalidKeys, "NAX0 key derivation failed.");
            }

            if (HeaderLength + Util.AlignUp(Header.Size, 0x10) > fileSize)
            {
                ThrowHelper.ThrowResult(ResultFs.AesXtsFileTooShort, "NAX0 key derivation failed.");
            }

            IStorage encStorage = BaseFile.AsStorage().Slice(HeaderLength, Util.AlignUp(Header.Size, 0x10));
            BaseStorage = new CachedStorage(new Aes128XtsStorage(encStorage, Header.DecryptedKey1, Header.DecryptedKey2, BlockSize, true), 4, true);
        }

        public byte[] GetKey()
        {
            var key = new byte[0x20];
            Array.Copy(Header.DecryptedKey1, 0, key, 0, 0x10);
            Array.Copy(Header.DecryptedKey2, 0, key, 0x10, 0x10);

            return key;
        }

        public override Result Read(out long bytesRead, long offset, Span<byte> destination, ReadOption options)
        {
            bytesRead = default;

            int toRead = ValidateReadParamsAndGetSize(destination, offset);

            Result rc = BaseStorage.Read(offset, destination.Slice(0, toRead));
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        public override Result Write(long offset, ReadOnlySpan<byte> source, WriteOption options)
        {
            ValidateWriteParams(source, offset);

            Result rc = BaseStorage.Write(offset, source);
            if (rc.IsFailure()) return rc;

            if ((options & WriteOption.Flush) != 0)
            {
                return Flush();
            }

            return Result.Success;
        }

        public override Result Flush()
        {
            return BaseStorage.Flush();
        }

        public override Result GetSize(out long size)
        {
            size = Header.Size;
            return Result.Success;
        }

        public override Result SetSize(long size)
        {
            Header.SetSize(size, VerificationKey);

            Result rc = BaseFile.Write(0, Header.ToBytes(false));
            if (rc.IsFailure()) return rc;

            return BaseStorage.SetSize(size);
        }
    }
}
