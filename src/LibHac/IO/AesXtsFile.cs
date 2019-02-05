using System;

namespace LibHac.IO
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

            if (!Header.TryDecryptHeader(Path, KekSeed, VerificationKey))
            {
                throw new ArgumentException("NAX0 key derivation failed.");
            }

            IStorage encStorage = BaseFile.AsStorage().Slice(HeaderLength, Header.Size);
            BaseStorage = new CachedStorage(new Aes128XtsStorage(encStorage, Header.DecryptedKey1, Header.DecryptedKey2, BlockSize, true), 4, true);
        }

        public override int Read(Span<byte> destination, long offset)
        {
            int toRead = ValidateReadParamsAndGetSize(destination, offset);

            BaseStorage.Read(destination.Slice(0, toRead), offset);

            return toRead;
        }

        public override void Write(ReadOnlySpan<byte> source, long offset)
        {
            ValidateWriteParams(source, offset);

            BaseStorage.Write(source, offset);
        }

        public override void Flush()
        {
            BaseStorage.Flush();
        }

        public override long GetSize()
        {
            return Header.Size;
        }

        public override void SetSize(long size)
        {
            throw new NotImplementedException();
        }
    }
}
