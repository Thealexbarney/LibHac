using LibHac.Crypto;

namespace LibHac.Gc;

public static class Values
{
    public const int GcPageSize = 0x200;
    public const int GcAsicFirmwareSize = 1024 * 30; // 30 KiB
    public const int GcCardDeviceIdSize = 0x10;
    public const int GcChallengeCardExistenceResponseSize = 0x58;
    public const int GcCardImageHashSize = 0x20;
    public const int GcDeviceCertificateSize = 0x200;
    public const int GcCardKeyAreaSize = GcCardKeyAreaPageCount * GcPageSize;
    public const int GcCardKeyAreaPageCount = 8;
    public const int GcCertAreaPageAddress = 56;
    public const int GcAesCbcIvLength = Aes.KeySize128;

    public const long UnusedAreaSizeBase = 1024 * 1024 * 72; // 72 MiB
    public const long MemorySizeBase = 1024 * 1024 * 1024; // 1 GiB
    public const long AvailableSizeBase = MemorySizeBase - UnusedAreaSizeBase;
}