using LibHac.Common.FixedArrays;

namespace LibHac.Fs;

public struct CodeVerificationData
{
    public Array256<byte> Signature;
    public Array32<byte> Hash;
    public bool HasData;
    public Array3<byte> Reserved;
}