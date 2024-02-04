using System;
using LibHac.Diag;
using LibHac.Fs;

namespace LibHac.FsSrv.Impl;

public static class SaveDataProperties
{
    public const long DefaultSaveDataBlockSize = 0x4000;
    public const long BcatSaveDataJournalSize = 0x200000;

    public static bool IsJournalingSupported(SaveDataFormatType type)
    {
        switch (type)
        {
            case SaveDataFormatType.Normal:
                return true;
            case SaveDataFormatType.NoJournal:
                return false;
            default:
                Abort.UnexpectedDefault();
                return default;
        }
    }

    public static bool IsJournalingSupported(SaveDataType type)
    {
        switch (type)
        {
            case SaveDataType.System:
            case SaveDataType.Account:
            case SaveDataType.Bcat:
            case SaveDataType.Device:
            case SaveDataType.Cache:
                return true;
            case SaveDataType.Temporary:
                return false;
            default:
                Abort.UnexpectedDefault();
                return default;
        }
    }

    public static bool IsMultiCommitSupported(SaveDataType type)
    {
        switch (type)
        {
            case SaveDataType.System:
            case SaveDataType.Account:
            case SaveDataType.Device:
                return true;
            case SaveDataType.Bcat:
            case SaveDataType.Temporary:
            case SaveDataType.Cache:
                return false;
            default:
                Abort.UnexpectedDefault();
                return default;
        }
    }

    public static bool IsSharedOpenNeeded(SaveDataType type)
    {
        switch (type)
        {
            case SaveDataType.System:
            case SaveDataType.Bcat:
            case SaveDataType.Temporary:
            case SaveDataType.Cache:
                return false;
            case SaveDataType.Account:
            case SaveDataType.Device:
                return true;
            default:
                Abort.UnexpectedDefault();
                return default;
        }
    }

    public static bool CanUseIndexerReservedArea(SaveDataType type)
    {
        switch (type)
        {
            case SaveDataType.System:
                return true;
            case SaveDataType.Account:
            case SaveDataType.Bcat:
            case SaveDataType.Device:
            case SaveDataType.Temporary:
            case SaveDataType.Cache:
                return false;
            default:
                Abort.UnexpectedDefault();
                return default;
        }
    }

    public static bool IsSystemSaveData(SaveDataType type)
    {
        switch (type)
        {
            case SaveDataType.System:
                return true;
            case SaveDataType.Account:
            case SaveDataType.Bcat:
            case SaveDataType.Device:
            case SaveDataType.Temporary:
            case SaveDataType.Cache:
                return false;
            default:
                Abort.UnexpectedDefault();
                return default;
        }
    }

    public static bool IsObsoleteSystemSaveData(in SaveDataInfo info)
    {
        foreach (ulong id in ObsoleteSystemSaveDataIdList)
        {
            if (info.StaticSaveDataId == id)
                return true;
        }

        return false;
    }

    public static bool IsWipingNeededAtCleanUp(in SaveDataInfo info)
    {
        switch (info.Type)
        {
            case SaveDataType.System:
                break;
            case SaveDataType.Account:
            case SaveDataType.Bcat:
            case SaveDataType.Device:
            case SaveDataType.Temporary:
            case SaveDataType.Cache:
                return true;
            default:
                Abort.UnexpectedDefault();
                break;
        }

        foreach (ulong id in SystemSaveDataIdWipingExceptionList)
        {
            if (info.StaticSaveDataId == id)
                return false;
        }

        return true;
    }

    public static bool IsValidSpaceIdForSaveDataMover(SaveDataType type, SaveDataSpaceId spaceId)
    {
        switch (type)
        {
            case SaveDataType.System:
            case SaveDataType.Account:
            case SaveDataType.Bcat:
            case SaveDataType.Device:
            case SaveDataType.Temporary:
                return false;
            case SaveDataType.Cache:
                return spaceId == SaveDataSpaceId.User || spaceId == SaveDataSpaceId.SdUser;
            default:
                Abort.UnexpectedDefault();
                return default;
        }
    }

    public static bool IsReconstructible(SaveDataType type, SaveDataSpaceId spaceId)
    {
        switch (spaceId)
        {
            case SaveDataSpaceId.System:
            case SaveDataSpaceId.User:
            case SaveDataSpaceId.ProperSystem:
            case SaveDataSpaceId.SafeMode:
                switch (type)
                {
                    case SaveDataType.System:
                    case SaveDataType.Account:
                    case SaveDataType.Device:
                        return false;
                    case SaveDataType.Bcat:
                    case SaveDataType.Temporary:
                    case SaveDataType.Cache:
                        return true;
                    default:
                        Abort.UnexpectedDefault();
                        return default;
                }
            case SaveDataSpaceId.SdSystem:
            case SaveDataSpaceId.Temporary:
            case SaveDataSpaceId.SdUser:
                return true;
            default:
                Abort.UnexpectedDefault();
                return default;
        }
    }

    private static ReadOnlySpan<ulong> ObsoleteSystemSaveDataIdList => [0x8000000000000060, 0x8000000000000075];

    private static ReadOnlySpan<ulong> SystemSaveDataIdWipingExceptionList =>
    [
        0x8000000000000040, 0x8000000000000041, 0x8000000000000043, 0x8000000000000044, 0x8000000000000045,
        0x8000000000000046, 0x8000000000000047, 0x8000000000000048, 0x8000000000000049, 0x800000000000004A,
        0x8000000000000070, 0x8000000000000071, 0x8000000000000072, 0x8000000000000074, 0x8000000000000076,
        0x8000000000000077, 0x8000000000000090, 0x8000000000000091, 0x8000000000000092, 0x80000000000000B0,
        0x80000000000000C1, 0x80000000000000C2, 0x8000000000000120, 0x8000000000000121, 0x8000000000000180,
        0x8000000000010003, 0x8000000000010004
    ];
}