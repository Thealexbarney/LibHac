namespace LibHac.Sdmmc
{
    public enum SpeedMode
    {
        MmcIdentification = 0,
        MmcLegacySpeed = 1,
        MmcHighSpeed = 2,
        MmcHs200 = 3,
        MmcHs400 = 4,
        SdCardIdentification = 5,
        SdCardDefaultSpeed = 6,
        SdCardHighSpeed = 7,
        SdCardSdr12 = 8,
        SdCardSdr25 = 9,
        SdCardSdr50 = 10,
        SdCardSdr104 = 11,
        SdCardDdr50 = 12,
        GcAsicFpgaSpeed = 13,
        GcAsicSpeed = 14
    }
}
