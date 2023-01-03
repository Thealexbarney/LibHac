using System;
using LibHac.Crypto.Impl;

namespace LibHac.Crypto;

public class Sha256Generator : IHash
{
    public const int HashSize = Sha256.DigestSize;

    private Sha256Impl _baseHash;

    public Sha256Generator()
    {
        _baseHash = new Sha256Impl();
    }

    public void Initialize()
    {
        _baseHash.Initialize();
    }

    public void Update(ReadOnlySpan<byte> data)
    {
        _baseHash.Update(data);
    }

    public void GetHash(Span<byte> hashBuffer)
    {
        _baseHash.GetHash(hashBuffer);
    }
}