using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Type = LibHac.Common.Keys.KeyInfo.KeyType;

namespace LibHac.Common.Keys
{
    internal static partial class DefaultKeySet
    {
        /// <summary>
        /// Creates a <see cref="KeySet"/> that contains any keys that have been embedded in the library.
        /// </summary>
        /// <returns>The created <see cref="KeySet"/>.</returns>
        public static KeySet CreateDefaultKeySet()
        {
            var keySet = new KeySet();

            // Fill the key set with any key structs included in the library.
            if (RootKeysDev.Length == Unsafe.SizeOf<RootKeys>())
            {
                keySet.KeyStruct._rootKeysDev = MemoryMarshal.Cast<byte, RootKeys>(RootKeysDev)[0];
            }

            if (RootKeysProd.Length == Unsafe.SizeOf<RootKeys>())
            {
                keySet.KeyStruct._rootKeysProd = MemoryMarshal.Cast<byte, RootKeys>(RootKeysProd)[0];
            }

            if (KeySeeds.Length == Unsafe.SizeOf<KeySeeds>())
            {
                keySet.KeyStruct._keySeeds = MemoryMarshal.Cast<byte, KeySeeds>(KeySeeds)[0];
            }

            if (StoredKeysDev.Length == Unsafe.SizeOf<StoredKeys>())
            {
                keySet.KeyStruct._storedKeysDev = MemoryMarshal.Cast<byte, StoredKeys>(StoredKeysDev)[0];
            }

            if (StoredKeysProd.Length == Unsafe.SizeOf<StoredKeys>())
            {
                keySet.KeyStruct._storedKeysProd = MemoryMarshal.Cast<byte, StoredKeys>(StoredKeysProd)[0];
            }

            if (DerivedKeysDev.Length == Unsafe.SizeOf<DerivedKeys>())
            {
                keySet.KeyStruct._derivedKeysDev = MemoryMarshal.Cast<byte, DerivedKeys>(DerivedKeysDev)[0];
            }

            if (DerivedKeysProd.Length == Unsafe.SizeOf<DerivedKeys>())
            {
                keySet.KeyStruct._derivedKeysProd = MemoryMarshal.Cast<byte, DerivedKeys>(DerivedKeysProd)[0];
            }

            if (DeviceKeys.Length == Unsafe.SizeOf<DeviceKeys>())
            {
                keySet.KeyStruct._deviceKeys = MemoryMarshal.Cast<byte, DeviceKeys>(DeviceKeys)[0];
            }

            if (RsaSigningKeysDev.Length == Unsafe.SizeOf<RsaSigningKeys>())
            {
                keySet.KeyStruct._rsaSigningKeysDev = MemoryMarshal.Cast<byte, RsaSigningKeys>(RsaSigningKeysDev)[0];
            }

            if (RsaSigningKeysProd.Length == Unsafe.SizeOf<RsaSigningKeys>())
            {
                keySet.KeyStruct._rsaSigningKeysProd = MemoryMarshal.Cast<byte, RsaSigningKeys>(RsaSigningKeysProd)[0];
            }

            if (RsaKeys.Length == Unsafe.SizeOf<RsaKeys>())
            {
                keySet.KeyStruct._rsaKeys = MemoryMarshal.Cast<byte, RsaKeys>(RsaKeys)[0];
            }

            return keySet;
        }

        /// <summary>
        /// Creates a <see cref="List{T}"/> of the <see cref="KeyInfo"/> of all keys that are loadable by default.
        /// </summary>
        /// <returns>The created list.</returns>
        public static List<KeyInfo> CreateKeyList()
        {
            // Update this value if more keys are added
            var keys = new List<KeyInfo>(70);

            // Keys with a group value of -1 are keys that will be read but not written.
            // This is for compatibility since some keys had other names in the past.

            // TSEC secrets aren't public yet, so the TSEC root keys will be treated as
            // root keys even though they're derived.

            keys.Add(new KeyInfo(10, Type.DeviceRoot, "secure_boot_key", (set, _) => set.SecureBootKey));
            keys.Add(new KeyInfo(11, Type.DeviceRoot, "tsec_key", (set, _) => set.TsecKey));
            keys.Add(new KeyInfo(12, Type.DeviceDrvd, "device_key", (set, _) => set.DeviceKey));

            keys.Add(new KeyInfo(20, Type.CommonRoot, "tsec_root_kek", (set, _) => set.TsecRootKek));
            keys.Add(new KeyInfo(21, Type.CommonRoot, "package1_mac_kek", (set, _) => set.Package1MacKek));
            keys.Add(new KeyInfo(22, Type.CommonRoot, "package1_kek", (set, _) => set.Package1Kek));

            keys.Add(new KeyInfo(30, Type.CommonRoot, "tsec_auth_signature", 0, 0x20, (set, i) => set.TsecAuthSignatures[i]));

            keys.Add(new KeyInfo(40, Type.CommonRoot, "tsec_root_key", 0, 0x20, (set, i) => set.TsecRootKeys[i]));

            keys.Add(new KeyInfo(50, Type.CommonSeed, "keyblob_mac_key_source", (set, _) => set.KeyBlobMacKeySource));
            keys.Add(new KeyInfo(51, Type.CommonSeed, "keyblob_key_source", 0, 6, (set, i) => set.KeyBlobKeySources[i]));

            keys.Add(new KeyInfo(55, Type.DeviceDrvd, "keyblob_key", 0, 6, (set, i) => set.KeyBlobKeys[i]));

            keys.Add(new KeyInfo(60, Type.DeviceDrvd, "keyblob_mac_key", 0, 6, (set, i) => set.KeyBlobMacKeys[i]));

            keys.Add(new KeyInfo(70, Type.DeviceRoot, "encrypted_keyblob", 0, 6, (set, i) => set.EncryptedKeyBlobs[i].Bytes));

            keys.Add(new KeyInfo(80, Type.CommonRoot, "keyblob", 0, 6, (set, i) => set.KeyBlobs[i].Bytes));

            keys.Add(new KeyInfo(90, Type.CommonSeed, "master_kek_source", 6, 0x20, (set, i) => set.MasterKekSources[i]));

            keys.Add(new KeyInfo(100, Type.CommonRoot, "mariko_bek", (set, _) => set.MarikoBek));
            keys.Add(new KeyInfo(101, Type.CommonRoot, "mariko_kek", (set, _) => set.MarikoKek));

            keys.Add(new KeyInfo(110, Type.CommonRoot, "mariko_aes_class_key", 0, 0xC, (set, i) => set.MarikoAesClassKeys[i]));
            keys.Add(new KeyInfo(120, Type.CommonSeedDiff, "mariko_master_kek_source", 0, 0x20, (set, i) => set.MarikoMasterKekSources[i]));
            keys.Add(new KeyInfo(130, Type.CommonDrvd, "master_kek", 0, 0x20, (set, i) => set.MasterKeks[i]));
            keys.Add(new KeyInfo(140, Type.CommonSeed, "master_key_source", (set, _) => set.MasterKeySource));
            keys.Add(new KeyInfo(150, Type.CommonDrvd, "master_key", 0, 0x20, (set, i) => set.MasterKeys[i]));

            keys.Add(new KeyInfo(160, Type.CommonDrvd, "package1_key", 0, 0x20, (set, i) => set.Package1Keys[i]));
            keys.Add(new KeyInfo(170, Type.CommonDrvd, "package1_mac_key", 6, 0x20, (set, i) => set.Package1MacKeys[i]));
            keys.Add(new KeyInfo(180, Type.CommonSeed, "package2_key_source", (set, _) => set.Package2KeySource));
            keys.Add(new KeyInfo(190, Type.CommonDrvd, "package2_key", 0, 0x20, (set, i) => set.Package2Keys[i]));

            keys.Add(new KeyInfo(200, Type.CommonSeed, "bis_kek_source", (set, _) => set.BisKekSource));
            keys.Add(new KeyInfo(201, Type.CommonSeed, "bis_key_source", 0, 4, (set, i) => set.BisKeySources[i]));

            keys.Add(new KeyInfo(205, Type.DeviceDrvd, "bis_key", 0, 4, (set, i) => set.BisKeys[i]));

            keys.Add(new KeyInfo(210, Type.CommonSeed, "per_console_key_source", (set, _) => set.PerConsoleKeySource));
            keys.Add(new KeyInfo(211, Type.CommonSeed, "retail_specific_aes_key_source", (set, _) => set.RetailSpecificAesKeySource));
            keys.Add(new KeyInfo(212, Type.CommonSeed, "aes_kek_generation_source", (set, _) => set.AesKekGenerationSource));
            keys.Add(new KeyInfo(213, Type.CommonSeed, "aes_key_generation_source", (set, _) => set.AesKeyGenerationSource));
            keys.Add(new KeyInfo(214, Type.CommonSeed, "titlekek_source", (set, _) => set.TitleKekSource));

            keys.Add(new KeyInfo(220, Type.CommonDrvd, "titlekek", 0, 0x20, (set, i) => set.TitleKeks[i]));

            keys.Add(new KeyInfo(230, Type.CommonSeed, "header_kek_source", (set, _) => set.HeaderKekSource));
            keys.Add(new KeyInfo(231, Type.CommonSeed, "header_key_source", (set, _) => set.HeaderKeySource));
            keys.Add(new KeyInfo(232, Type.CommonDrvd, "header_key", (set, _) => set.HeaderKey));

            keys.Add(new KeyInfo(240, Type.CommonSeed, "key_area_key_application_source", (set, _) => set.KeyAreaKeyApplicationSource));
            keys.Add(new KeyInfo(241, Type.CommonSeed, "key_area_key_ocean_source", (set, _) => set.KeyAreaKeyOceanSource));
            keys.Add(new KeyInfo(242, Type.CommonSeed, "key_area_key_system_source", (set, _) => set.KeyAreaKeySystemSource));

            keys.Add(new KeyInfo(250, Type.CommonSeed, "save_mac_kek_source", (set, _) => set.DeviceUniqueSaveMacKekSource));
            keys.Add(new KeyInfo(251, Type.CommonSeed, "save_mac_key_source", 0, 2, (set, i) => set.DeviceUniqueSaveMacKeySources[i]));
            keys.Add(new KeyInfo(252, Type.DeviceDrvd, "save_mac_key", 0, 2, (set, i) => set.DeviceUniqueSaveMacKeys[i]));
            keys.Add(new KeyInfo(-01, Type.CommonSeed, "save_mac_key_source", (set, _) => set.DeviceUniqueSaveMacKeySources[0]));

            keys.Add(new KeyInfo(253, Type.CommonSeed, "save_mac_sd_card_kek_source", (set, _) => set.SeedUniqueSaveMacKekSource));
            keys.Add(new KeyInfo(254, Type.CommonSeed, "save_mac_sd_card_key_source", (set, _) => set.SeedUniqueSaveMacKeySource));
            keys.Add(new KeyInfo(255, Type.DeviceDrvd, "save_mac_sd_card_key", (set, _) => set.SeedUniqueSaveMacKey));

            keys.Add(new KeyInfo(260, Type.DeviceRoot, "sd_seed", (set, _) => set.SdCardEncryptionSeed));

            keys.Add(new KeyInfo(261, Type.CommonSeed, "sd_card_kek_source", (set, _) => set.SdCardKekSource));
            keys.Add(new KeyInfo(262, Type.CommonSeed, "sd_card_save_key_source", (set, _) => set.SdCardKeySources[0]));
            keys.Add(new KeyInfo(263, Type.CommonSeed, "sd_card_nca_key_source", (set, _) => set.SdCardKeySources[1]));
            keys.Add(new KeyInfo(264, Type.CommonSeed, "sd_card_custom_storage_key_source", (set, _) => set.SdCardKeySources[2]));

            keys.Add(new KeyInfo(270, Type.CommonSeedDiff, "xci_header_key", (set, _) => set.XciHeaderKey));

            keys.Add(new KeyInfo(280, Type.CommonRoot, "eticket_rsa_kek", (set, _) => set.ETicketRsaKek));
            keys.Add(new KeyInfo(281, Type.CommonRoot, "ssl_rsa_kek", (set, _) => set.SslRsaKek));

            keys.Add(new KeyInfo(290, Type.CommonDrvd, "key_area_key_application", 0, 0x20, (set, i) => set.KeyAreaKeys[i][0]));
            keys.Add(new KeyInfo(300, Type.CommonDrvd, "key_area_key_ocean", 0, 0x20, (set, i) => set.KeyAreaKeys[i][1]));
            keys.Add(new KeyInfo(310, Type.CommonDrvd, "key_area_key_system", 0, 0x20, (set, i) => set.KeyAreaKeys[i][2]));

            return keys;
        }
    }
}
