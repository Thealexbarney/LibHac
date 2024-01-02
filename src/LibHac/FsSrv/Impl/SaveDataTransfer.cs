using System;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Fs;
using LibHac.Fs.Impl;
using LibHac.FsSystem;

namespace LibHac.FsSrv.Impl;

public interface IChunkEncryptorFactory : IDisposable
{
    Result Create(ref SharedRef<AesGcmSource.IEncryptor> outEncryptor);
}

public interface IChunkDecryptorFactory : IDisposable
{
    Result Create(ref SharedRef<AesGcmSink.IDecryptor> outDecryptor, in AesMac mac);
}

public struct InitialDataVersion1Detail
{
    public Content DataContent;
    public Array16<byte> Mac;

    public struct Content
    {
        public SaveDataExtraData ExtraData;
        public uint Version;
        public Array104<byte> Reserved;
    }
}

public struct InitialDataVersion2Detail
{
    public AesGcmStreamHeader GcmStreamHeader;
    public Content DataContent;
    public AesGcmStreamTail GcmStreamTail;

    public struct Content
    {
        public uint Signature;
        public uint Version;
        public SaveDataExtraData ExtraData;
        public int DivisionCount;
        public long DivisionAlignment;
        public Array64<Hash> ChunkHashes;
        public Array64<long> ChunkSizes;
        public Array64<AesMac> ChunkMacs;
        public long TotalChunkSize;
        public InitialDataAad InitialDataAad;
        public HashSalt HashSalt;
        public Array128<ShortHash> ShortHashes;
        public bool IsIntegritySeedEnabled;
        public HashAlgorithmType HashAlgorithmType;
        public Array3438<byte> Reserved;
    }

    public struct Hash
    {
        public Array32<byte> Data;
    }

    public struct ShortHash
    {
        public Array4<byte> Data;
    }
}