using System;

namespace LibHac.Fs
{
    public static class GameCard
    {
        public static long GetGameCardSizeBytes(GameCardSizeInternal size) => size switch
        {
            GameCardSizeInternal.Size1Gb => 0x3B800000,
            GameCardSizeInternal.Size2Gb => 0x77000000,
            GameCardSizeInternal.Size4Gb => 0xEE000000,
            GameCardSizeInternal.Size8Gb => 0x1DC000000,
            GameCardSizeInternal.Size16Gb => 0x3B8000000,
            GameCardSizeInternal.Size32Gb => 0x770000000,
            _ => 0
        };

        public static long CardPageToOffset(int page)
        {
            return (long)page << 9;
        }
    }

    public enum GameCardSizeInternal : byte
    {
        Size1Gb = 0xFA,
        Size2Gb = 0xF8,
        Size4Gb = 0xF0,
        Size8Gb = 0xE0,
        Size16Gb = 0xE1,
        Size32Gb = 0xE2
    }

    [Flags]
    public enum GameCardAttribute : byte
    {
        None = 0,
        AutoBootFlag = 1 << 0,
        HistoryEraseFlag = 1 << 1,
        RepairToolFlag = 1 << 2,
        DifferentRegionCupToTerraDeviceFlag = 1 << 3,
        DifferentRegionCupToGlobalDeviceFlag = 1 << 4
    }
}
