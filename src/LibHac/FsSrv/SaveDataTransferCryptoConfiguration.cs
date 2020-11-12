using System;
using LibHac.Crypto;

namespace LibHac.FsSrv
{
    public class SaveDataTransferCryptoConfiguration
    {
        private Data100 _tokenSigningKeyModulus;
        private Data100 _keySeedPackageSigningKeyModulus;
        private Data100 _kekEncryptionKeyModulus;
        private Data100 _keyPackageSigningModulus;

        public Span<byte> TokenSigningKeyModulus => _tokenSigningKeyModulus.Data;
        public Span<byte> KeySeedPackageSigningKeyModulus => _keySeedPackageSigningKeyModulus.Data;
        public Span<byte> KekEncryptionKeyModulus => _kekEncryptionKeyModulus.Data;
        public Span<byte> KeyPackageSigningModulus => _keyPackageSigningModulus.Data;

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
}
