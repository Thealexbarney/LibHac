using System.Diagnostics;
using LibHac.Common.FixedArrays;
using LibHac.Util;

namespace LibHac.Fs;

[DebuggerDisplay("{ToString()}")]
public struct EncryptionSeed
{
    public Array16<byte> Value;

    public readonly override string ToString() => Value.ItemsRo.ToHexString();
}