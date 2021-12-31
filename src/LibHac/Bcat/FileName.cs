using System;
using LibHac.Common.FixedArrays;
using LibHac.Util;

namespace LibHac.Bcat;

public struct FileName
{
    private const int MaxSize = 0x20;

    public Array32<byte> Value;

    public readonly bool IsValid()
    {
        ReadOnlySpan<byte> name = Value.ItemsRo;

        int i;
        for (i = 0; i < name.Length; i++)
        {
            if (name[i] == 0)
                break;

            if (!StringUtils.IsDigit(name[i]) && !StringUtils.IsAlpha(name[i]) && name[i] != '_' && name[i] != '.')
                return false;
        }

        if (i == 0 || i == MaxSize)
            return false;

        if (name[i] != 0)
            return false;

        return name[i - 1] != '.';
    }

    public readonly override string ToString()
    {
        return StringUtils.Utf8ZToString(Value.ItemsRo);
    }
}