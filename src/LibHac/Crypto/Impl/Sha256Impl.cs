using System;
using System.Diagnostics;
using System.Security.Cryptography;
using LibHac.Common;

namespace LibHac.Crypto.Impl
{
    public struct Sha256Impl
    {
        private SHA256 _baseHash;
        private HashState _state;

        public void Initialize()
        {
            if (_state == HashState.Initial)
            {
                _baseHash = SHA256.Create();
            }
            else
            {
                _baseHash.Initialize();
            }

            _state = HashState.Initialized;
        }

        public void Update(ReadOnlySpan<byte> data)
        {
            Debug.Assert(_state == HashState.Initialized);

            using var rented = new RentedArray<byte>(data.Length);

            data.CopyTo(rented.Span);

            _baseHash.TransformBlock(rented.Array, 0, data.Length, null, 0);
        }

        public void GetHash(Span<byte> hashBuffer)
        {
            Debug.Assert(_state == HashState.Initialized || _state == HashState.Done);
            Debug.Assert(hashBuffer.Length >= Sha256.DigestSize);

            if (_state == HashState.Initialized)
            {
                _baseHash.TransformFinalBlock(new byte[0], 0, 0);
                _state = HashState.Done;
            }

            _baseHash.Hash.CopyTo(hashBuffer);
        }
    }
}
