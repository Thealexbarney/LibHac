using LibHac.Crypto;
using Xunit;

namespace LibHac.Tests.CryptoTests
{
    public sealed class AesIntrinsicsRequiredTheoryAttribute : TheoryAttribute
    {
        public AesIntrinsicsRequiredTheoryAttribute()
        {
            if (!Aes.IsAesNiSupported())
            {
                Skip = "AES intrinsics required";
            }
        }
    }
}