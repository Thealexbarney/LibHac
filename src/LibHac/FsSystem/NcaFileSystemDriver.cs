using LibHac.Common.FixedArrays;
using LibHac.Crypto;

namespace LibHac.FsSystem;

public struct NcaCryptoConfiguration
{
    public static readonly int Rsa2048KeyModulusSize = Rsa.ModulusSize2048Pss;
    public static readonly int Rsa2048KeyPublicExponentSize = Rsa.MaximumExponentSize2048Pss;
    public static readonly int Rsa2048KeyPrivateExponentSize = Rsa2048KeyModulusSize;

    public static readonly int Aes128KeySize = Aes.KeySize128;

    public static readonly int Header1SignatureKeyGenerationMax = 1;

    public static readonly int KeyAreaEncryptionKeyIndexCount = 3;
    public static readonly int HeaderEncryptionKeyCount = 2;

    public static readonly int KeyGenerationMax = 32;
    public static readonly int KeyAreaEncryptionKeyCount = KeyAreaEncryptionKeyIndexCount * KeyGenerationMax;

    public Array2<Array256<byte>> Header1SignKeyModuli;
    public Array3<byte> Header1SignKeyPublicExponent;
    public Array3<Array16<byte>> KeyAreaEncryptionKeySources;
    public Array16<byte> HeaderEncryptionKeySource;
    public Array2<Array16<byte>> HeaderEncryptedEncryptionKeys;
    public GenerateKeyFunction GenerateKey;
    public DecryptAesCtrFunction DecryptAesCtr;
    public DecryptAesCtrFunction DecryptAesCtrForExternalKey;
    public bool IsDev;
}

public struct NcaCompressionConfiguration
{
    public GetDecompressorFunction GetDecompressorFunc;
}

public static class NcaKeyFunctions
{
    public static bool IsInvalidKeyTypeValue(int keyType)
    {
        return keyType < 0;
    }

    public static int GetKeyTypeValue(byte keyIndex, byte keyGeneration)
    {
        const int invalidKeyTypeValue = -1;

        if (keyIndex >= NcaCryptoConfiguration.KeyAreaEncryptionKeyIndexCount)
            return invalidKeyTypeValue;

        return NcaCryptoConfiguration.KeyAreaEncryptionKeyIndexCount * keyGeneration + keyIndex;
    }
}

public enum KeyType
{
    NcaHeaderKey = 0x60,
    NcaExternalKey = 0x61,
    SaveDataDeviceUniqueMac = 0x62,
    SaveDataSeedUniqueMac = 0x63,
    SaveDataTransferMac = 0x64
}