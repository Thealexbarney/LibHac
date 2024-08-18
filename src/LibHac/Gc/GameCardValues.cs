using LibHac.Crypto;

namespace LibHac.Gc;

public static class Values
{
    public const int GcPageSize = 0x200;

    public const int GcCardKeyAreaPageCount = 8;
    public const int GcCardKeyAreaSize = GcCardKeyAreaPageCount * GcPageSize;

    public const int GcCardHeaderPageAddress = 0;
    public const int GcCardHeaderPageCount = 1;

    public const int GcReservedAreaPageCount = 0x37;

    public const int GcCertAreaPageAddress = GcCardHeaderPageCount + GcReservedAreaPageCount;
    public const int GcDeviceCertificatePageCount = 1;
    public const int GcCertAreaPageCount = 0x40;

    public const int GcPackageIdSize = 8;
    public const int GcPartitionFsHeaderHashSize = 0x20;
    public const int GcCardDeviceIdSize = 0x10;
    public const int GcDeviceCertificateSize = 0x200;
    public const int GcCardImageHashSize = 0x20;

    public const int GcChallengeCardExistenceResponseSize = 0x58;
    public const int GcChallengeCardExistenceSeedSize = 0xF;
    public const int GcChallengeCardExistenceValueSize = 0x10;

    public const int GcMmcCmd60DataSize = 0x40;

    public const int GcAsicFirmwareSize = 1024 * 30; // 30 KiB

    public const int GcAsicSerialNumberLength = 0x10;

    public const int GcRandomValueSize = 32;
    public const int GcRandomValueForKeyUpdateSocSize = 31;
    public const int GcRsaOaepSeedSize = 32;

    public const int GcAesBlockLength = Aes.BlockSize;

    public const int GcRsaKeyLength = Rsa.ModulusSize2048Pss;
    public const int GcRsaPublicExponentLength = Rsa.MaximumExponentSize2048Pss;
    public const int GcAesKeyLength = Aes.KeySize128;
    public const int GcAesCbcIvLength = Aes.KeySize128;
    public const int GcHmacKeyLength = 0x20;
    public const int GcCvConstLength = 0x10;
    public const int GcTitleKeyKekIndexMax = 0x10;
    public const int GcSha256HashLength = Sha256.DigestSize;

    public const int GcSendCommandMaxCount = 3;

    public const byte GcPaddingU8 = 0xFF;
    public const int GcCertificateSize = 0x400;
    public const int GcSocModulusOffset = 0x130;
    public const int GcCertificateSetSize = GcCertificateSize + GcAsicSerialNumberLength + GcRsaKeyLength + GcRsaPublicExponentLength;

    public const long UnusedAreaSizeBase = 1024 * 1024 * 72; // 72 MiB
    public const long MemorySizeBase = 1024 * 1024 * 1024; // 1 GiB
    public const long AvailableSizeBase = MemorySizeBase - UnusedAreaSizeBase;

    public const int Cmd60DefaultTimeOutMilliSeconds = 3500;
    public const int EraseTimeOutMilliSeconds = 10 * 1000;
}