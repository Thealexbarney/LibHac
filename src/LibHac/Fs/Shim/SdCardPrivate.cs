using System;
using LibHac.Diag;

namespace LibHac.Fs.Shim;

public static class SdCardPrivate
{
    public const int SdCardCidSize = 16;
    public const int SdCardCidPnmSize = 5;
    public const int SdCardCidOidSize = 2;

    private const int SdCardCidMdtMonthCodeIndex = 0;
    private const int SdCardCidMdtYearCodeLowerIndex = 0;
    private const int SdCardCidMdtYearCodeUpperIndex = 1;
    private const int SdCardCidPsnIndex = 2;
    private const int SdCardCidPrvIndex = 6;
    private const int SdCardCidPnmIndex = 7;
    private const int SdCardCidOidIndex = 12;
    private const int SdCardCidMidIndex = 14;

    public static int GetMdtMonthCodeFromSdCardCid(ReadOnlySpan<byte> cid)
    {
        Abort.DoAbortUnless(cid.Length >= SdCardCidSize);

        return cid[SdCardCidMdtMonthCodeIndex] & 0xF;
    }

    public static int GetMdtYearCodeFromSdCardCid(ReadOnlySpan<byte> cid)
    {
        Abort.DoAbortUnless(cid.Length >= SdCardCidSize);

        return ((cid[SdCardCidMdtYearCodeUpperIndex] & 0xF) << 4) | ((cid[SdCardCidMdtYearCodeLowerIndex] & 0xF0) >> 4);
    }

    public static int GetPsnFromSdCardCid(ReadOnlySpan<byte> cid)
    {
        Abort.DoAbortUnless(cid.Length >= SdCardCidSize);

        return (cid[SdCardCidPsnIndex + 3] << 24) |
               (cid[SdCardCidPsnIndex + 2] << 16) |
               (cid[SdCardCidPsnIndex + 1] << 8) |
               cid[SdCardCidPsnIndex];
    }

    public static int GetPrvFromSdCardCid(ReadOnlySpan<byte> cid)
    {
        Abort.DoAbortUnless(cid.Length >= SdCardCidSize);

        return cid[SdCardCidPrvIndex];
    }

    public static void GetPnmFromSdCardCid(Span<byte> destination, ReadOnlySpan<byte> cid)
    {
        Abort.DoAbortUnless(cid.Length >= SdCardCidSize);
        Abort.DoAbortUnless(destination.Length >= SdCardCidPnmSize);

        for (int i = 0; i < SdCardCidPnmSize; i++)
        {
            destination[SdCardCidPnmSize - i - 1] = cid[SdCardCidPnmIndex + i];
        }
    }

    public static void GetOidFromSdCardCid(Span<byte> destination, ReadOnlySpan<byte> cid)
    {
        Abort.DoAbortUnless(cid.Length >= SdCardCidSize);
        Abort.DoAbortUnless(destination.Length >= SdCardCidOidSize);

        for (int i = 0; i < SdCardCidOidSize; i++)
        {
            destination[SdCardCidOidSize - i - 1] = cid[SdCardCidOidIndex + i];
        }
    }

    public static int GetMidFromSdCardCid(ReadOnlySpan<byte> cid)
    {
        Abort.DoAbortUnless(cid.Length >= SdCardCidSize);

        return cid[SdCardCidMidIndex];
    }
}