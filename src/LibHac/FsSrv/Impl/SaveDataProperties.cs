using LibHac.Diag;
using LibHac.Fs;

namespace LibHac.FsSrv.Impl
{
    public static class SaveDataProperties
    {
        public const long DefaultSaveDataBlockSize = 0x4000;
        public const long BcatSaveDataJournalSize = 0x200000;

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
                case SaveDataType.SystemBcat:
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
                case SaveDataType.SystemBcat:
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
    }
}
