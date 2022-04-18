using System;
using LibHac.Common.FixedArrays;
using LibHac.FsSystem;

namespace LibHac.FsSrv;

public class SaveDataTransferCryptoConfiguration
{
    private Array256<byte> _tokenSigningKeyModulus;
    private Array256<byte> _keySeedPackageSigningKeyModulus;
    private Array256<byte> _kekEncryptionKeyModulus;
    private Array256<byte> _keyPackageSigningModulus;

    public Span<byte> TokenSigningKeyModulus => _tokenSigningKeyModulus.Items;
    public Span<byte> KeySeedPackageSigningKeyModulus => _keySeedPackageSigningKeyModulus.Items;
    public Span<byte> KekEncryptionKeyModulus => _kekEncryptionKeyModulus.Items;
    public Span<byte> KeyPackageSigningModulus => _keyPackageSigningModulus.Items;

    public SaveTransferAesKeyGenerator GenerateAesKey { get; set; }
    public RandomDataGenerator GenerateRandomData { get; set; }
    public SaveTransferCmacGenerator GenerateCmac { get; set; }

    public enum KeyIndex
    {
        SaveDataTransferToken,
        SaveDataTransfer,
        SaveDataTransferKeySeedPackage,
        CloudBackUpInitialData,
        CloudBackUpImportContext,
        CloudBackUpInitialDataMac,
        SaveDataRepairKeyPackage,
        SaveDataRepairInitialDataMacBeforeRepair,
        SaveDataRepairInitialDataMacAfterRepair
    }
}