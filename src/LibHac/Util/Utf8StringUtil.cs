using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Diag;

namespace LibHac.Util;

/// <summary>
/// Contains functions for verifying and copying UTF-8 strings.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0</remarks>
public static class Utf8StringUtil
{
    private static ReadOnlySpan<byte> CodePointByteLengthTable => new byte[]
    {
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
        3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        4, 4, 4, 4, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
    };

    public static bool VerifyUtf8String(U8Span str)
    {
        return GetCodePointCountOfUtf8String(str) != -1;
    }

    public static int GetCodePointCountOfUtf8String(U8Span str)
    {
        Assert.SdkRequiresGreater(str.Length, 0);

        ReadOnlySpan<byte> currentStr = str.Value;
        int codePointCount = 0;

        while (currentStr.Length != 0)
        {
            int codePointByteLength = GetCodePointByteLength(currentStr[0]);

            if (codePointByteLength > currentStr.Length)
                return -1;

            if (!VerifyCode(currentStr.Slice(0, codePointByteLength)))
                return -1;

            currentStr = currentStr.Slice(codePointByteLength);

            codePointCount++;
        }

        return codePointCount;
    }

    public static int CopyUtf8String(Span<byte> output, ReadOnlySpan<byte> input, int maxCount)
    {
        Assert.SdkRequiresGreater(output.Length, 0);
        Assert.SdkRequiresGreater(input.Length, 0);
        Assert.SdkRequiresGreater(maxCount, 0);

        ReadOnlySpan<byte> currentInput = input;
        int remainingCount = maxCount;

        while (remainingCount > 0 && currentInput.Length != 0)
        {
            // Verify the current code point
            int codePointLength = GetCodePointByteLength(currentInput[0]);
            if (codePointLength > currentInput.Length)
                break;

            if (!VerifyCode(currentInput.Slice(0, codePointLength)))
                break;

            // Ensure the output is large enough to hold the additional code point
            int currentOutputLength =
                Unsafe.ByteOffset(ref MemoryMarshal.GetReference(input), ref MemoryMarshal.GetReference(currentInput))
                    .ToInt32() + codePointLength;

            if (currentOutputLength + 1 > output.Length)
                break;

            // Advance to the next code point
            currentInput = currentInput.Slice(codePointLength);
            remainingCount--;
        }

        // Copy the valid UTF-8 to the output buffer
        int byteLength = Unsafe
            .ByteOffset(ref MemoryMarshal.GetReference(input), ref MemoryMarshal.GetReference(currentInput)).ToInt32();

        Assert.SdkAssert(byteLength + 1 <= output.Length);

        if (byteLength != 0)
            input.Slice(0, byteLength).CopyTo(output);

        output[byteLength] = 0;
        return byteLength;
    }

    private static int GetCodePointByteLength(byte head)
    {
        return CodePointByteLengthTable[head];
    }

    private static bool IsValidTail(byte tail)
    {
        return (tail & 0xC0) == 0x80;
    }

    private static bool VerifyCode(ReadOnlySpan<byte> str)
    {
        if (str.Length == 1)
            return true;

        switch (str.Length)
        {
            case 2:
                if (!IsValidTail(str[1]))
                    return false;

                break;
            case 3:
                if (str[0] == 0xE0 && (str[1] & 0x20) == 0)
                    return false;

                if (str[0] == 0xED && (str[1] & 0x20) != 0)
                    return false;

                if (!IsValidTail(str[1]) || !IsValidTail(str[2]))
                    return false;

                break;
            case 4:
                if (str[0] == 0xF0 && (str[1] & 0x30) == 0)
                    return false;

                if (str[0] == 0xFD && (str[1] & 0x30) != 0)
                    return false;

                if (!IsValidTail(str[1]) || !IsValidTail(str[2]) || !IsValidTail(str[3]))
                    return false;

                break;
            default:
                return false;
        }

        return true;
    }
}