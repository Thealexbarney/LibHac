using LibHac.Crypto2;
using Xunit;

namespace LibHac.Tests.CryptoTests
{
    public sealed class AesIntrinsicsRequiredTheoryAttribute : TheoryAttribute
    {
        public AesIntrinsicsRequiredTheoryAttribute()
        {
            if (!AesCrypto.IsAesNiSupported())
            {
                Skip = "AES intrinsics required";
            }
        }
    }
}