using System;
using System.Diagnostics.CodeAnalysis;
using LibHac.Common;
using LibHac.Common.FixedArrays;

namespace LibHac.Fs;

internal struct MountName
{
    private Array16<byte> _nameArray;
    [UnscopedRef] public Span<byte> Name => _nameArray;

    public override string ToString() => new U8Span(Name).ToString();
}