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
        return false;
        throw new NotImplementedException();
    }

    public static bool IsWipingNeededAtCleanUp(in SaveDataInfo info)
    {
        return false;
        throw new NotImplementedException();
    }

    public static bool IsValidSpaceIdForSaveDataMover(SaveDataType type, SaveDataSpaceId spaceId)
    {
        throw new NotImplementedException();
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
}