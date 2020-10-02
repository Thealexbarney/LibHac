using System;
using System.Runtime.InteropServices;
using LibHac.Boot;
using LibHac.Common.FixedArrays;
using LibHac.Crypto;
using LibHac.FsSrv;

namespace LibHac.Common.Keys
{
    public class KeySet
    {
        /// <summary>
        /// The number of keyblobs that were used for &lt; 6.2.0 crypto
        /// </summary>
        private const int UsedKeyBlobCount = 6;
        private const int SdCardKeyIdCount = 3;
        private const int KeyRevisionCount = 0x20;

        private AllKeys _keys;
        public ref AllKeys KeyStruct => ref _keys;

        public ExternalKeySet ExternalKeySet { get; } = new ExternalKeySet();

        public Span<AesKey> MarikoAesClassKeys => _keys._rootKeys.MarikoAesClassKeys.Items;
        public ref AesKey MarikoKek => ref _keys._rootKeys.MarikoKek;
        public ref AesKey MarikoBek => ref _keys._rootKeys.MarikoBek;
        public Span<KeyBlob> KeyBlobs => _keys._rootKeys.KeyBlobs.Items;
        public Span<AesKey> KeyBlobKeySources => _keys._keySeeds.KeyBlobKeySources.Items;
        public ref AesKey KeyBlobMacKeySource => ref _keys._keySeeds.KeyBlobMacKeySource;
        public ref AesKey TsecRootKek => ref _keys._rootKeys.TsecRootKek;
        public ref AesKey Package1MacKek => ref _keys._rootKeys.Package1MacKek;
        public ref AesKey Package1Kek => ref _keys._rootKeys.Package1Kek;
        public Span<AesKey> TsecAuthSignatures => _keys._rootKeys.TsecAuthSignatures.Items;
        public Span<AesKey> TsecRootKeys => _keys._rootKeys.TsecRootKeys.Items;
        public Span<AesKey> MasterKekSources => _keys._keySeeds.MasterKekSources.Items;
        public Span<AesKey> MarikoMasterKekSources => _keys._keySeeds.MarikoMasterKekSources.Items;
        public Span<AesKey> MasterKeks => _keys._derivedKeys.MasterKeks.Items;
        public ref AesKey MasterKeySource => ref _keys._keySeeds.MasterKeySource;
        public Span<AesKey> MasterKeys => _keys._derivedKeys.MasterKeys.Items;
        public Span<AesKey> Package1MacKeys => _keys._derivedKeys.Package1MacKeys.Items;
        public Span<AesKey> Package1Keys => _keys._derivedKeys.Package1Keys.Items;
        public Span<AesKey> Package2Keys => _keys._derivedKeys.Package2Keys.Items;
        public ref AesKey Package2KeySource => ref _keys._keySeeds.Package2KeySource;
        public ref AesKey PerConsoleKeySource => ref _keys._keySeeds.PerConsoleKeySource;
        public ref AesKey RetailSpecificAesKeySource => ref _keys._keySeeds.RetailSpecificAesKeySource;
        public ref AesKey BisKekSource => ref _keys._keySeeds.BisKekSource;
        public Span<AesXtsKey> BisKeySources => _keys._keySeeds.BisKeySources.Items;
        public ref AesKey AesKekGenerationSource => ref _keys._keySeeds.AesKekGenerationSource;
        public ref AesKey AesKeyGenerationSource => ref _keys._keySeeds.AesKeyGenerationSource;
        public ref AesKey KeyAreaKeyApplicationSource => ref _keys._keySeeds.KeyAreaKeyApplicationSource;
        public ref AesKey KeyAreaKeyOceanSource => ref _keys._keySeeds.KeyAreaKeyOceanSource;
        public ref AesKey KeyAreaKeySystemSource => ref _keys._keySeeds.KeyAreaKeySystemSource;
        public ref AesKey TitleKekSource => ref _keys._keySeeds.TitleKekSource;
        public ref AesKey HeaderKekSource => ref _keys._keySeeds.HeaderKekSource;
        public ref AesKey SdCardKekSource => ref _keys._keySeeds.SdCardKekSource;
        public Span<AesXtsKey> SdCardKeySources => _keys._keySeeds.SdCardKeySources.Items;
        public ref AesKey DeviceUniqueSaveMacKekSource => ref _keys._keySeeds.DeviceUniqueSaveMacKekSource;
        public Span<AesKey> DeviceUniqueSaveMacKeySources => _keys._keySeeds.DeviceUniqueSaveMacKeySources.Items;
        public ref AesKey SeedUniqueSaveMacKekSource => ref _keys._keySeeds.SeedUniqueSaveMacKekSource;
        public ref AesKey SeedUniqueSaveMacKeySource => ref _keys._keySeeds.SeedUniqueSaveMacKeySource;
        public ref AesXtsKey HeaderKeySource => ref _keys._keySeeds.HeaderKeySource;
        public ref AesXtsKey HeaderKey => ref _keys._derivedKeys.HeaderKey;
        public Span<AesKey> TitleKeks => _keys._derivedKeys.TitleKeks.Items;
        public Span<Array3<AesKey>> KeyAreaKeys => _keys._derivedKeys.KeyAreaKeys.Items;
        public ref AesKey XciHeaderKey => ref _keys._rootKeys.XciHeaderKey;
        public ref AesKey EticketRsaKek => ref _keys._derivedKeys.EticketRsaKek;
        public ref AesKey SslRsaKek => ref _keys._derivedKeys.SslRsaKek;

        public ref AesKey SecureBootKey => ref _keys._deviceKeys.SecureBootKey;
        public ref AesKey TsecKey => ref _keys._deviceKeys.TsecKey;
        public Span<AesKey> KeyBlobKeys => _keys._deviceKeys.KeyBlobKeys.Items;
        public Span<AesKey> KeyBlobMacKeys => _keys._deviceKeys.KeyBlobMacKeys.Items;
        public Span<EncryptedKeyBlob> EncryptedKeyBlobs => _keys._deviceKeys.EncryptedKeyBlobs.Items;
        public ref AesKey DeviceKey => ref _keys._deviceKeys.DeviceKey;
        public Span<AesXtsKey> BisKeys => _keys._deviceKeys.BisKeys.Items;
        public Span<AesKey> DeviceUniqueSaveMacKeys => _keys._deviceKeys.DeviceUniqueSaveMacKeys.Items;
        public ref AesKey SeedUniqueSaveMacKey => ref _keys._deviceKeys.SeedUniqueSaveMacKey;
        public ref AesKey SdCardEncryptionSeed => ref _keys._deviceKeys.SdCardEncryptionSeed;
        public Span<AesXtsKey> SdCardEncryptionKeys => _keys._deviceKeys.SdCardEncryptionKeys.Items;

        public void SetSdSeed(ReadOnlySpan<byte> sdSeed)
        {
            if (sdSeed.Length != 0x10)
                throw new ArgumentException("Sd card encryption seed must be 16 bytes long.");

            sdSeed.CopyTo(SdCardEncryptionSeed);
            DeriveSdCardKeys();
        }

        public void DeriveKeys(IProgressReport logger = null)
        {
            DeriveKeyBlobKeys();
            DecryptKeyBlobs(logger);
            ReadKeyBlobs();

            Derive620MasterKeks();
            DeriveMarikoMasterKeks();
            DeriveMasterKeys();

            DerivePerConsoleKeys();
            DerivePerFirmwareKeys();
            DeriveNcaHeaderKey();
            DeriveSdCardKeys();
        }

        private void DeriveKeyBlobKeys()
        {
            if (SecureBootKey.IsEmpty() || TsecKey.IsEmpty()) return;

            bool haveKeyBlobMacKeySource = !MasterKeySource.IsEmpty();
            var temp = new AesKey();

            for (int i = 0; i < UsedKeyBlobCount; i++)
            {
                if (KeyBlobKeySources[i].IsEmpty()) continue;

                Aes.DecryptEcb128(KeyBlobKeySources[i], temp, TsecKey);
                Aes.DecryptEcb128(temp, KeyBlobKeys[i], SecureBootKey);

                if (!haveKeyBlobMacKeySource) continue;

                Aes.DecryptEcb128(KeyBlobMacKeySource, KeyBlobMacKeys[i], KeyBlobKeys[i]);
            }
        }

        private void DecryptKeyBlobs(IProgressReport logger = null)
        {
            var cmac = new AesCmac();

            for (int i = 0; i < UsedKeyBlobCount; i++)
            {
                if (KeyBlobKeys[i].IsEmpty() || KeyBlobMacKeys[i].IsEmpty() || EncryptedKeyBlobs[i].IsEmpty())
                {
                    continue;
                }

                Aes.CalculateCmac(cmac, EncryptedKeyBlobs[i].Bytes.Slice(0x10, 0xA0), KeyBlobMacKeys[i]);

                if (!Utilities.SpansEqual<byte>(cmac, EncryptedKeyBlobs[i].Cmac))
                {
                    logger?.LogMessage($"Warning: Keyblob MAC {i:x2} is invalid. Are SBK/TSEC key correct?");
                }

                Aes.DecryptCtr128(EncryptedKeyBlobs[i].Bytes.Slice(0x20), KeyBlobs[i].Bytes, KeyBlobKeys[i],
                    EncryptedKeyBlobs[i].Counter);
            }
        }

        private void ReadKeyBlobs()
        {
            for (int i = 0; i < UsedKeyBlobCount; i++)
            {
                if (KeyBlobs[i].IsEmpty()) continue;

                MasterKeks[i] = KeyBlobs[i].MasterKek;
                Package1Keys[i] = KeyBlobs[i].Package1Key;
            }
        }

        private void Derive620MasterKeks()
        {
            for (int i = UsedKeyBlobCount; i < KeyRevisionCount; i++)
            {
                // Key revisions >= 8 all use the same TSEC root key
                int tsecRootKeyIndex = Math.Min(i, 8) - UsedKeyBlobCount;
                if (TsecRootKeys[tsecRootKeyIndex].IsEmpty() || MasterKekSources[i].IsEmpty()) continue;

                Aes.DecryptEcb128(MasterKekSources[i], MasterKeks[i], TsecRootKeys[tsecRootKeyIndex]);
            }
        }

        private void DeriveMarikoMasterKeks()
        {
            if (MarikoKek.IsEmpty()) return;

            for (int i = 0; i < KeyRevisionCount; i++)
            {
                if (MarikoMasterKekSources[i].IsEmpty()) continue;

                Aes.DecryptEcb128(MarikoMasterKekSources[i], MasterKeks[i], MarikoKek);
            }
        }

        private void DeriveMasterKeys()
        {
            if (MasterKeySource.IsEmpty()) return;

            for (int i = 0; i < KeyRevisionCount; i++)
            {
                if (MasterKeks[i].IsEmpty()) continue;

                Aes.DecryptEcb128(MasterKeySource, MasterKeys[i], MasterKeks[i]);
            }
        }

        private void DerivePerConsoleKeys()
        {
            var kek = new AesKey();

            // Derive the device key
            if (!PerConsoleKeySource.IsEmpty() && !KeyBlobKeys[0].IsEmpty())
            {
                Aes.DecryptEcb128(PerConsoleKeySource, DeviceKey, KeyBlobKeys[0]);
            }

            // Derive device-unique save keys
            for (int i = 0; i < DeviceUniqueSaveMacKeySources.Length; i++)
            {
                if (!DeviceUniqueSaveMacKekSource.IsEmpty() && !DeviceUniqueSaveMacKeySources[i].IsEmpty() &&
                    !DeviceKey.IsEmpty())
                {
                    GenerateKek(DeviceKey, DeviceUniqueSaveMacKekSource, kek, AesKekGenerationSource, null);
                    Aes.DecryptEcb128(DeviceUniqueSaveMacKeySources[i], DeviceUniqueSaveMacKeys[i], kek);
                }
            }

            // Derive BIS keys
            if (DeviceKey.IsEmpty()
                || BisKekSource.IsEmpty()
                || AesKekGenerationSource.IsEmpty()
                || AesKeyGenerationSource.IsEmpty()
                || RetailSpecificAesKeySource.IsEmpty())
            {
                return;
            }

            // If the user doesn't provide bis_key_source_03 we can assume it's the same as bis_key_source_02
            if (BisKeySources[3].IsEmpty() && !BisKeySources[2].IsEmpty())
            {
                BisKeySources[3] = BisKeySources[2];
            }

            Aes.DecryptEcb128(RetailSpecificAesKeySource, kek, DeviceKey);
            if (!BisKeySources[0].IsEmpty()) Aes.DecryptEcb128(BisKeySources[0], BisKeys[0], kek);

            GenerateKek(DeviceKey, BisKekSource, kek, AesKekGenerationSource, AesKeyGenerationSource);

            for (int i = 1; i < 4; i++)
            {
                if (!BisKeySources[i].IsEmpty())
                    Aes.DecryptEcb128(BisKeySources[i], BisKeys[i], kek);
            }
        }

        private void DerivePerFirmwareKeys()
        {
            bool haveKakSource0 = !KeyAreaKeyApplicationSource.IsEmpty();
            bool haveKakSource1 = !KeyAreaKeyOceanSource.IsEmpty();
            bool haveKakSource2 = !KeyAreaKeySystemSource.IsEmpty();
            bool haveTitleKekSource = !TitleKekSource.IsEmpty();
            bool havePackage2KeySource = !Package2KeySource.IsEmpty();

            for (int i = 0; i < KeyRevisionCount; i++)
            {
                if (MasterKeys[i].IsEmpty())
                {
                    continue;
                }

                if (haveKakSource0)
                {
                    GenerateKek(MasterKeys[i], KeyAreaKeyApplicationSource, KeyAreaKeys[i][0],
                        AesKekGenerationSource, AesKeyGenerationSource);
                }

                if (haveKakSource1)
                {
                    GenerateKek(MasterKeys[i], KeyAreaKeyOceanSource, KeyAreaKeys[i][1],
                        AesKekGenerationSource, AesKeyGenerationSource);
                }

                if (haveKakSource2)
                {
                    GenerateKek(MasterKeys[i], KeyAreaKeySystemSource, KeyAreaKeys[i][2],
                        AesKekGenerationSource, AesKeyGenerationSource);
                }

                if (haveTitleKekSource)
                {
                    Aes.DecryptEcb128(TitleKekSource, TitleKeks[i], MasterKeys[i]);
                }

                if (havePackage2KeySource)
                {
                    Aes.DecryptEcb128(Package2KeySource, Package2Keys[i], MasterKeys[i]);
                }
            }
        }

        private void DeriveNcaHeaderKey()
        {
            if (HeaderKekSource.IsEmpty() || HeaderKeySource.IsEmpty() || MasterKeys[0].IsEmpty()) return;

            var headerKek = new AesKey();

            GenerateKek(MasterKeys[0], HeaderKekSource, headerKek, AesKekGenerationSource,
                AesKeyGenerationSource);
            Aes.DecryptEcb128(HeaderKeySource, HeaderKey, headerKek);
        }

        public void DeriveSdCardKeys()
        {
            var sdKek = new AesKey();
            var tempKey = new AesXtsKey();
            GenerateKek(MasterKeys[0], SdCardKekSource, sdKek, AesKekGenerationSource, AesKeyGenerationSource);

            for (int k = 0; k < SdCardKeyIdCount; k++)
            {
                for (int i = 0; i < 4; i++)
                {
                    tempKey.Data64[i] = SdCardKeySources[k].Data64[i] ^ SdCardEncryptionSeed.Data64[i & 1];
                }

                tempKey.Data64[0] = SdCardKeySources[k].Data64[0] ^ SdCardEncryptionSeed.Data64[0];
                tempKey.Data64[1] = SdCardKeySources[k].Data64[1] ^ SdCardEncryptionSeed.Data64[1];
                tempKey.Data64[2] = SdCardKeySources[k].Data64[2] ^ SdCardEncryptionSeed.Data64[0];
                tempKey.Data64[3] = SdCardKeySources[k].Data64[3] ^ SdCardEncryptionSeed.Data64[1];

                Aes.DecryptEcb128(tempKey, SdCardEncryptionKeys[k], sdKek);
            }

            // Derive sd card save key
            if (!SeedUniqueSaveMacKekSource.IsEmpty() && !SeedUniqueSaveMacKeySource.IsEmpty())
            {
                var keySource = new AesKey();

                keySource.Data64[0] = SeedUniqueSaveMacKeySource.Data64[0] ^ SdCardEncryptionSeed.Data64[0];
                keySource.Data64[1] = SeedUniqueSaveMacKeySource.Data64[1] ^ SdCardEncryptionSeed.Data64[1];

                GenerateKek(MasterKeys[0], SeedUniqueSaveMacKekSource, sdKek, AesKekGenerationSource, null);
                Aes.DecryptEcb128(keySource, SeedUniqueSaveMacKey, sdKek);
            }
        }

        private static void GenerateKek(ReadOnlySpan<byte> key, ReadOnlySpan<byte> src, Span<byte> dest,
            ReadOnlySpan<byte> kekSeed, ReadOnlySpan<byte> keySeed)
        {
            var kek = new AesKey();
            var srcKek = new AesKey();

            Aes.DecryptEcb128(kekSeed, kek, key);
            Aes.DecryptEcb128(src, srcKek, kek);

            if (!keySeed.IsEmpty)
            {
                Aes.DecryptEcb128(keySeed, dest, srcKek);
            }
            else
            {
                srcKek.Data.CopyTo(dest);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AllKeys
    {
        public RootKeys _rootKeys;
        public KeySeeds _keySeeds;
        public DerivedKeys _derivedKeys;
        public DeviceKeys _deviceKeys;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RootKeys
    {
        public Array12<AesKey> MarikoAesClassKeys;
        public AesKey MarikoKek;
        public AesKey MarikoBek;
        public Array32<KeyBlob> KeyBlobs;
        public AesKey TsecRootKek;
        public AesKey Package1MacKek;
        public AesKey Package1Kek;
        public Array32<AesKey> TsecAuthSignatures;
        public Array32<AesKey> TsecRootKeys;
        public AesKey XciHeaderKey;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KeySeeds
    {
        public Array32<AesKey> KeyBlobKeySources;
        public AesKey KeyBlobMacKeySource;
        public Array32<AesKey> MasterKekSources;
        public Array32<AesKey> MarikoMasterKekSources;
        public AesKey MasterKeySource;
        public AesKey Package2KeySource;
        public AesKey PerConsoleKeySource;
        public AesKey RetailSpecificAesKeySource;
        public AesKey BisKekSource;
        public Array4<AesXtsKey> BisKeySources;
        public AesKey AesKekGenerationSource;
        public AesKey AesKeyGenerationSource;
        public AesKey KeyAreaKeyApplicationSource;
        public AesKey KeyAreaKeyOceanSource;
        public AesKey KeyAreaKeySystemSource;
        public AesKey TitleKekSource;
        public AesKey HeaderKekSource;
        public AesKey SdCardKekSource;
        public Array3<AesXtsKey> SdCardKeySources;
        public AesKey DeviceUniqueSaveMacKekSource;
        public Array2<AesKey> DeviceUniqueSaveMacKeySources;
        public AesKey SeedUniqueSaveMacKekSource;
        public AesKey SeedUniqueSaveMacKeySource;
        public AesXtsKey HeaderKeySource;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DerivedKeys
    {
        public Array32<AesKey> MasterKeks;
        public Array32<AesKey> MasterKeys;
        public Array32<AesKey> Package1MacKeys;
        public Array32<AesKey> Package1Keys;
        public Array32<AesKey> Package2Keys;
        public Array32<Array3<AesKey>> KeyAreaKeys;
        public Array32<AesKey> TitleKeks;
        public AesXtsKey HeaderKey;
        public AesKey EticketRsaKek;
        public AesKey SslRsaKek;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DeviceKeys
    {
        public AesKey SecureBootKey;
        public AesKey TsecKey;
        public Array32<AesKey> KeyBlobKeys;
        public Array32<AesKey> KeyBlobMacKeys;
        public Array32<EncryptedKeyBlob> EncryptedKeyBlobs;
        public AesKey DeviceKey;
        public Array4<AesXtsKey> BisKeys;
        public Array2<AesKey> DeviceUniqueSaveMacKeys;
        public AesKey SeedUniqueSaveMacKey;
        public AesKey SdCardEncryptionSeed;
        public Array3<AesXtsKey> SdCardEncryptionKeys;
    }
}
