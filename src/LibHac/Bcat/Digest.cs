using LibHac.Common.FixedArrays;
using LibHac.Util;

namespace LibHac.Bcat;

public struct Digest
{
    public Array16<byte> Value;

    public readonly override string ToString()
    {
        return Value[..].ToHexString();
    }
}