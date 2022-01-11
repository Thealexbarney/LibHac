using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using LibHac.Common.FixedArrays;
using LibHac.Diag;
using LibHac.Util;

namespace LibHac.Fs;

[DebuggerDisplay("{DebugDisplay(),nq}")]
public struct RightsId : IEquatable<RightsId>
{
    public Array16<byte> Value;

    public RightsId(ReadOnlySpan<byte> value)
    {
        Assert.Equal(0x10, value.Length);

        Unsafe.SkipInit(out Value);

        Span<ulong> longsThis = MemoryMarshal.Cast<byte, ulong>(Value.Items);
        ReadOnlySpan<ulong> longsValue = MemoryMarshal.Cast<byte, ulong>(value);

        longsThis[1] = longsValue[1];
        longsThis[0] = longsValue[0];
    }

    public readonly override string ToString() => Value.ItemsRo.ToHexString();

    public readonly string DebugDisplay()
    {
        ReadOnlySpan<byte> highBytes = Value.ItemsRo.Slice(0, 8);
        ReadOnlySpan<byte> lowBytes = Value.ItemsRo.Slice(8, 8);

        return $"{highBytes.ToHexString()} {lowBytes.ToHexString()}";
    }

    public readonly bool Equals(RightsId other)
    {
        return Unsafe.As<Array16<byte>, Vector128<byte>>(ref Unsafe.AsRef(in Value))
            .Equals(Unsafe.As<Array16<byte>, Vector128<byte>>(ref other.Value));
    }

    public readonly override bool Equals(object obj) => obj is RightsId other && Equals(other);

    public readonly override int GetHashCode()
    {
        ReadOnlySpan<ulong> longSpan = MemoryMarshal.Cast<byte, ulong>(Value.ItemsRo);
        return HashCode.Combine(longSpan[0], longSpan[1]);
    }

    public static bool operator ==(RightsId left, RightsId right) => left.Equals(right);
    public static bool operator !=(RightsId left, RightsId right) => !left.Equals(right);
}