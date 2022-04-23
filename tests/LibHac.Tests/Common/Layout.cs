using System;
using System.Runtime.CompilerServices;

namespace LibHac.Tests.Common;

public class Layout
{
    public static int GetOffset<TStruct, TField>(in TStruct structRef, in TField fieldRef)
    {
        ref TField structOffset = ref Unsafe.As<TStruct, TField>(ref Unsafe.AsRef(in structRef));
        ref TField fieldOffset = ref Unsafe.AsRef(in fieldRef);
        int offset = Unsafe.ByteOffset(ref structOffset, ref fieldOffset).ToInt32();

        if (offset >= Unsafe.SizeOf<TStruct>())
            throw new ArgumentException($"Error getting field offset. {nameof(structRef)} and {nameof(fieldRef)} must be from the same struct instance.");

        return offset;
    }

    public static int AlignOf<T>() where T : unmanaged
    {
        return Unsafe.SizeOf<AlignOfHelper<T>>() - Unsafe.SizeOf<T>();
    }

    private readonly struct AlignOfHelper<T> where T : unmanaged
    {
#pragma warning disable CS0169 // Field is never used
        private readonly byte _padding;
        private readonly T _value;
#pragma warning restore CS0169
    }
}