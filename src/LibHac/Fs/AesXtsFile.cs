﻿using System;

namespace LibHac.Fs
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
                ThrowHelper.ThrowResult(ResultFs.AesXtsFileHeaderInvalidKeys, "NAX0 key derivation failed.");
            }

            if (HeaderLength + Util.AlignUp(Header.Size, 0x10) > baseFile.GetSize())
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

        public override int Read(Span<byte> destination, long offset, ReadOption options)
        {
            int toRead = ValidateReadParamsAndGetSize(destination, offset);

            BaseStorage.Read(destination.Slice(0, toRead), offset);

            return toRead;
        }

        public override void Write(ReadOnlySpan<byte> source, long offset, WriteOption options)
        {
            ValidateWriteParams(source, offset);

            BaseStorage.Write(source, offset);

            if ((options & WriteOption.Flush) != 0)
            {
                Flush();
            }
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
            Header.SetSize(size, VerificationKey);

            BaseFile.Write(Header.ToBytes(false), 0);

            BaseStorage.SetSize(size);
        }
    }
}
