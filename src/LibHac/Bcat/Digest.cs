using System.Diagnostics;
using LibHac.Common.FixedArrays;
using LibHac.Util;

namespace LibHac.Bcat;

[DebuggerDisplay("{ToString()}")]
public struct Digest
{
    public Array16<byte> Value;

    public readonly override string ToString()
    {
        return Value.ItemsRo.ToHexString();
    }
}