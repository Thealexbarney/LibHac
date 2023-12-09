using System;
using LibHac.Common.FixedArrays;
using LibHac.Util;

namespace LibHac.Bcat;

public struct DirectoryName
{
    private const int MaxSize = 0x20;

    public Array32<byte> Value;

    public readonly bool IsValid()
    {
        ReadOnlySpan<byte> name = Value;

        int i;
        for (i = 0; i < name.Length; i++)
        {
            if (name[i] == 0)
                break;

            if (!StringUtils.IsDigit(name[i]) && !StringUtils.IsAlpha(name[i]) && name[i] != '_' && name[i] != '-')
                return false;
        }

        if (i == 0 || i == MaxSize)
            return false;

        return name[i] == 0;
    }

    public readonly override string ToString()
    {
        return StringUtils.Utf8ZToString(Value);
    }
}