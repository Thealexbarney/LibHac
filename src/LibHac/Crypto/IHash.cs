using System;

namespace LibHac.Crypto
{
    public interface IHash
    {
        void Initialize();
        void Update(ReadOnlySpan<byte> data);
        void GetHash(Span<byte> hashBuffer);
    }
}