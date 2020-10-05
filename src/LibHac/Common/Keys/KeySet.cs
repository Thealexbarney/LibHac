using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using LibHac.Boot;
using LibHac.Common.FixedArrays;
using LibHac.Crypto;
using LibHac.FsSrv;
using Aes = LibHac.Crypto.Aes;

namespace LibHac.Common.Keys
{
    public class KeySet
    {
        public enum Mode
        {
            Dev,
            Prod
        }

        /// <summary>
        /// The number of keyblobs that were used for &lt; 6.2.0 crypto
        /// </summary>
        private const int UsedKeyBlobCount = 6;
        private const int SdCardKeyIdCount = 3;
        private const int KeyRevisionCount = 0x20;

        private AllKeys _keys;
        private Mode _mode = Mode.Prod;

        public ref AllKeys KeyStruct => ref _keys;
        public Mode CurrentMode => _mode;

        private ref RootKeys RootKeys => ref _mode == Mode.Dev ? ref _keys._rootKeysDev : ref _keys._rootKeysProd;
        private ref StoredKeys StoredKeys => ref _mode == Mode.Dev ? ref _keys._storedKeysDev : ref _keys._storedKeysProd;
        private ref DerivedKeys DerivedKeys => ref _mode == Mode.Dev ? ref _keys._derivedKeysDev : ref _keys._derivedKeysProd;
        private ref RsaSigningKeys RsaSigningKeys => ref _mode == Mode.Dev ? ref _keys._rsaSigningKeysDev : ref _keys._rsaSigningKeysProd;
        private ref RsaKeys RsaKeys => ref _keys._rsaKeys;

        private ref RsaSigningKeyParameters RsaSigningKeyParams => ref _mode == Mode.Dev
            ? ref _rsaSigningKeyParamsDev
            : ref _rsaSigningKeyParamsProd;

        public ExternalKeySet ExternalKeySet { get; } = new ExternalKeySet();

        public Span<AesKey> MarikoAesClassKeys => RootKeys.MarikoAesClassKeys.Items;
        public ref AesKey MarikoKek => ref RootKeys.MarikoKek;
        public ref AesKey MarikoBek => ref RootKeys.MarikoBek;
        public Span<KeyBlob> KeyBlobs => RootKeys.KeyBlobs.Items;
        public Span<AesKey> KeyBlobKeySources => _keys._keySeeds.KeyBlobKeySources.Items;
        public ref AesKey KeyBlobMacKeySource => ref _keys._keySeeds.KeyBlobMacKeySource;
        public ref AesKey TsecRootKek => ref RootKeys.TsecRootKek;
        public ref AesKey Package1MacKek => ref RootKeys.Package1MacKek;
        public ref AesKey Package1Kek => ref RootKeys.Package1Kek;
        public Span<AesKey> TsecAuthSignatures => RootKeys.TsecAuthSignatures.Items;
        public Span<AesKey> TsecRootKeys => RootKeys.TsecRootKeys.Items;
        public Span<AesKey> MasterKekSources => _keys._keySeeds.MasterKekSources.Items;

        public Span<AesKey> MarikoMasterKekSources => _mode == Mode.Dev
            ? _keys._keySeeds.MarikoMasterKekSources_dev.Items
            : _keys._keySeeds.MarikoMasterKekSources.Items;

        public Span<AesKey> MasterKeks => DerivedKeys.MasterKeks.Items;
        public ref AesKey MasterKeySource => ref _keys._keySeeds.MasterKeySource;
        public Span<AesKey> MasterKeys => DerivedKeys.MasterKeys.Items;
        public Span<AesKey> Package1MacKeys => DerivedKeys.Package1MacKeys.Items;
        public Span<AesKey> Package1Keys => DerivedKeys.Package1Keys.Items;
        public Span<AesKey> Package2Keys => DerivedKeys.Package2Keys.Items;
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
        public ref AesXtsKey HeaderKey => ref DerivedKeys.HeaderKey;
        public Span<AesKey> TitleKeks => DerivedKeys.TitleKeks.Items;
        public Span<Array3<AesKey>> KeyAreaKeys => DerivedKeys.KeyAreaKeys.Items;
        public ref AesKey XciHeaderKey => ref StoredKeys.XciHeaderKey;
        public ref AesKey ETicketRsaKek => ref DerivedKeys.ETicketRsaKek;
        public ref AesKey SslRsaKek => ref DerivedKeys.SslRsaKek;

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

        private RsaSigningKeyParameters _rsaSigningKeyParamsDev;
        private RsaSigningKeyParameters _rsaSigningKeyParamsProd;
        private RsaKeyParameters _rsaKeyParams;

        public Span<RSAParameters> NcaHeaderSigningKeys
        {
            get
            {
                ref Array2<RSAParameters>? keys = ref RsaSigningKeyParams.NcaHeaderSigningKeys;

                if (keys is null)
                {
                    keys = new Array2<RSAParameters>();
                    keys.Value[0] = CreateRsaParameters(in RsaSigningKeys.NcaHeaderSigningKeys[0]);
                    keys.Value[1] = CreateRsaParameters(in RsaSigningKeys.NcaHeaderSigningKeys[1]);
                }

                return keys.Value.Items;
            }
        }

        public Span<RSAParameters> AcidSigningKeys
        {
            get
            {
                ref Array2<RSAParameters>? keys = ref RsaSigningKeyParams.AcidSigningKeys;

                if (keys is null)
                {
                    keys = new Array2<RSAParameters>();
                    keys.Value[0] = CreateRsaParameters(in RsaSigningKeys.AcidSigningKeys[0]);
                    keys.Value[1] = CreateRsaParameters(in RsaSigningKeys.AcidSigningKeys[1]);
                }

                return keys.Value.Items;
            }
        }

        public ref RSAParameters Package2SigningKey
        {
            get
            {
                ref Array1<RSAParameters>? keys = ref RsaSigningKeyParams.Package2SigningKey;

                if (keys is null)
                {
                    keys = new Array1<RSAParameters>();
                    keys.Value[0] = CreateRsaParameters(in RsaSigningKeys.Package2SigningKey);
                }

                return ref keys.Value[0];
            }
        }

        public ref RSAParameters BetaNca0KeyAreaKey
        {
            get
            {
                ref Array1<RSAParameters>? keys = ref _rsaKeyParams.BetaNca0KeyAreaKey;

                if (keys is null)
                {
                    keys = new Array1<RSAParameters>();
                    keys.Value[0] = CreateRsaParameters(in RsaKeys.BetaNca0KeyAreaKey);
                }

                return ref keys.Value[0];
            }
        }

        public void SetSdSeed(ReadOnlySpan<byte> sdSeed)
        {
            if (sdSeed.Length != 0x10)
                throw new ArgumentException("Sd card encryption seed must be 16 bytes long.");

            sdSeed.CopyTo(SdCardEncryptionSeed);
            DeriveSdCardKeys();
        }

        public void SetMode(Mode mode)
        {
            _mode = mode;
        }

        public void DeriveKeys(IProgressReport logger = null)
        {
            DeriveKeyBlobKeys();
            DecryptKeyBlobs(logger);
            ReadKeyBlobs();

            Derive620Keys();
            Derive620MasterKeks();
            DeriveMarikoMasterKeks();
            DeriveMasterKeys();
            PopulateOldMasterKeys();

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

        private void Derive620Keys()
        {
            bool haveTsecRootKek = !TsecRootKek.IsEmpty();
            bool havePackage1MacKek = !Package1MacKek.IsEmpty();
            bool havePackage1Kek = !Package1Kek.IsEmpty();

            for (int i = UsedKeyBlobCount; i < KeyRevisionCount; i++)
            {
                if (TsecAuthSignatures[i - UsedKeyBlobCount].IsEmpty())
                    continue;

                if (haveTsecRootKek)
                {
                    Aes.EncryptEcb128(TsecAuthSignatures[i - UsedKeyBlobCount],
                        TsecRootKeys[i - UsedKeyBlobCount], TsecRootKek);
                }

                if (havePackage1MacKek)
                {
                    Aes.EncryptEcb128(TsecAuthSignatures[i - UsedKeyBlobCount],
                        Package1MacKeys[i], Package1MacKek);
                }

                if (havePackage1Kek)
                {
                    Aes.EncryptEcb128(TsecAuthSignatures[i - UsedKeyBlobCount],
                        Package1Keys[i], Package1Kek);
                }
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

        private void PopulateOldMasterKeys()
        {
            // Find the newest master key we have
            int newestMasterKey = -1;

            for (int i = MasterKeyVectors.Length - 1; i >= 0; i--)
            {
                if (!MasterKeys[i].IsEmpty())
                {
                    newestMasterKey = i;
                    break;
                }
            }

            if (newestMasterKey == -1)
                return;

            // Don't populate old master keys unless the newest master key is valid
            if (!TestKeyGeneration(newestMasterKey))
                return;

            // Decrypt all previous master keys
            for (int i = newestMasterKey; i > 0; i--)
            {
                Aes.DecryptEcb128(MasterKeyVectors[i], MasterKeys[i - 1], MasterKeys[i]);
            }
        }

        /// <summary>
        /// Check if the master key of the specified generation is correct.
        /// </summary>
        /// <param name="generation">The generation to test.</param>
        /// <returns><see langword="true"/> if the key is correct.</returns>
        private bool TestKeyGeneration(int generation)
        {
            // Decrypt the vector chain until we get Master Key 0
            AesKey key = MasterKeys[generation];

            for (int i = generation; i > 0; i--)
            {
                Aes.DecryptEcb128(MasterKeyVectors[i], key, key);
            }

            // Decrypt the zeros with Master Key 0
            Aes.DecryptEcb128(MasterKeyVectors[0], key, key);

            // If we don't get zeros, MasterKeys[generation] is incorrect
            return key.IsEmpty();
        }

        private ReadOnlySpan<AesKey> MasterKeyVectors =>
            MemoryMarshal.Cast<byte, AesKey>(_mode == Mode.Dev ? MasterKeyVectorsDev : MasterKeyVectorsProd);

        private static ReadOnlySpan<byte> MasterKeyVectorsDev => new byte[]
        {
            0x46, 0x22, 0xB4, 0x51, 0x9A, 0x7E, 0xA7, 0x7F, 0x62, 0xA1, 0x1F, 0x8F, 0xC5, 0x3A, 0xDB, 0xFE, // Zeroes encrypted with Master Key 00.
            0x39, 0x33, 0xF9, 0x31, 0xBA, 0xE4, 0xA7, 0x21, 0x2C, 0xDD, 0xB7, 0xD8, 0xB4, 0x4E, 0x37, 0x23, // Master key 00 encrypted with Master key 01.
            0x97, 0x29, 0xB0, 0x32, 0x43, 0x14, 0x8C, 0xA6, 0x85, 0xE9, 0x5A, 0x94, 0x99, 0x39, 0xAC, 0x5D, // Master key 01 encrypted with Master key 02.
            0x2C, 0xCA, 0x9C, 0x31, 0x1E, 0x07, 0xB0, 0x02, 0x97, 0x0A, 0xD8, 0x03, 0xA2, 0x76, 0x3F, 0xA3, // Master key 02 encrypted with Master key 03.
            0x9B, 0x84, 0x76, 0x14, 0x72, 0x94, 0x52, 0xCB, 0x54, 0x92, 0x9B, 0xC4, 0x8C, 0x5B, 0x0F, 0xBA, // Master key 03 encrypted with Master key 04.
            0x78, 0xD5, 0xF1, 0x20, 0x3D, 0x16, 0xE9, 0x30, 0x32, 0x27, 0x34, 0x6F, 0xCF, 0xE0, 0x27, 0xDC, // Master key 04 encrypted with Master key 05.
            0x6F, 0xD2, 0x84, 0x1D, 0x05, 0xEC, 0x40, 0x94, 0x5F, 0x18, 0xB3, 0x81, 0x09, 0x98, 0x8D, 0x4E, // Master key 05 encrypted with Master key 06.
            0x37, 0xAF, 0xAB, 0x35, 0x79, 0x09, 0xD9, 0x48, 0x29, 0xD2, 0xDB, 0xA5, 0xA5, 0xF5, 0x30, 0x19, // Master key 06 encrypted with Master key 07.
            0xEC, 0xE1, 0x46, 0x89, 0x37, 0xFD, 0xD2, 0x15, 0x8C, 0x3F, 0x24, 0x82, 0xEF, 0x49, 0x68, 0x04, // Master key 07 encrypted with Master key 08.
            0x43, 0x3D, 0xC5, 0x3B, 0xEF, 0x91, 0x02, 0x21, 0x61, 0x54, 0x63, 0x8A, 0x35, 0xE7, 0xCA, 0xEE, // Master key 08 encrypted with Master key 09.
            0x6C, 0x2E, 0xCD, 0xB3, 0x34, 0x61, 0x77, 0xF5, 0xF9, 0xB1, 0xDD, 0x61, 0x98, 0x19, 0x3E, 0xD4  // Master key 09 encrypted with Master key 0A.
        };

        private static ReadOnlySpan<byte> MasterKeyVectorsProd => new byte[]
        {
            0x0C, 0xF0, 0x59, 0xAC, 0x85, 0xF6, 0x26, 0x65, 0xE1, 0xE9, 0x19, 0x55, 0xE6, 0xF2, 0x67, 0x3D, // Zeroes encrypted with Master Key 00.
            0x29, 0x4C, 0x04, 0xC8, 0xEB, 0x10, 0xED, 0x9D, 0x51, 0x64, 0x97, 0xFB, 0xF3, 0x4D, 0x50, 0xDD, // Master key 00 encrypted with Master key 01.
            0xDE, 0xCF, 0xEB, 0xEB, 0x10, 0xAE, 0x74, 0xD8, 0xAD, 0x7C, 0xF4, 0x9E, 0x62, 0xE0, 0xE8, 0x72, // Master key 01 encrypted with Master key 02.
            0x0A, 0x0D, 0xDF, 0x34, 0x22, 0x06, 0x6C, 0xA4, 0xE6, 0xB1, 0xEC, 0x71, 0x85, 0xCA, 0x4E, 0x07, // Master key 02 encrypted with Master key 03.
            0x6E, 0x7D, 0x2D, 0xC3, 0x0F, 0x59, 0xC8, 0xFA, 0x87, 0xA8, 0x2E, 0xD5, 0x89, 0x5E, 0xF3, 0xE9, // Master key 03 encrypted with Master key 04.
            0xEB, 0xF5, 0x6F, 0x83, 0x61, 0x9E, 0xF8, 0xFA, 0xE0, 0x87, 0xD7, 0xA1, 0x4E, 0x25, 0x36, 0xEE, // Master key 04 encrypted with Master key 05.
            0x1E, 0x1E, 0x22, 0xC0, 0x5A, 0x33, 0x3C, 0xB9, 0x0B, 0xA9, 0x03, 0x04, 0xBA, 0xDB, 0x07, 0x57, // Master key 05 encrypted with Master key 06.
            0xA4, 0xD4, 0x52, 0x6F, 0xD1, 0xE4, 0x36, 0xAA, 0x9F, 0xCB, 0x61, 0x27, 0x1C, 0x67, 0x65, 0x1F, // Master key 06 encrypted with Master key 07.
            0xEA, 0x60, 0xB3, 0xEA, 0xCE, 0x8F, 0x24, 0x46, 0x7D, 0x33, 0x9C, 0xD1, 0xBC, 0x24, 0x98, 0x29, // Master key 07 encrypted with Master key 08.
            0x4D, 0xD9, 0x98, 0x42, 0x45, 0x0D, 0xB1, 0x3C, 0x52, 0x0C, 0x9A, 0x44, 0xBB, 0xAD, 0xAF, 0x80, // Master key 08 encrypted with Master key 09.
            0xB8, 0x96, 0x9E, 0x4A, 0x00, 0x0D, 0xD6, 0x28, 0xB3, 0xD1, 0xDB, 0x68, 0x5F, 0xFB, 0xE1, 0x2A  // Master key 09 encrypted with Master key 0A.
        };

        private void DerivePerConsoleKeys()
        {
            // Todo: Dev and newer key generations
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

        private static RSAParameters CreateRsaParameters(in RsaKey key)
        {
            return new RSAParameters
            {
                Exponent = key.PublicExponent.DataRo.ToArray(),
                Modulus = key.Modulus.DataRo.ToArray()
            };
        }

        private static RSAParameters CreateRsaParameters(in RsaFullKey key)
        {
            return new RSAParameters
            {
                D = key.PrivateExponent.DataRo.ToArray(),
                DP = key.Dp.DataRo.ToArray(),
                DQ = key.Dq.DataRo.ToArray(),
                Exponent = key.PublicExponent.DataRo.ToArray(),
                InverseQ = key.InverseQ.DataRo.ToArray(),
                Modulus = key.Modulus.DataRo.ToArray(),
                P = key.P.DataRo.ToArray(),
                Q = key.Q.DataRo.ToArray()
            };
        }

        private struct RsaSigningKeyParameters
        {
            public Array2<RSAParameters>? NcaHeaderSigningKeys;
            public Array2<RSAParameters>? AcidSigningKeys;
            public Array1<RSAParameters>? Package2SigningKey;
        }

        private struct RsaKeyParameters
        {
            public Array1<RSAParameters>? BetaNca0KeyAreaKey;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AllKeys
    {
        public RootKeys _rootKeysDev;
        public RootKeys _rootKeysProd;
        public KeySeeds _keySeeds;
        public StoredKeys _storedKeysDev;
        public StoredKeys _storedKeysProd;
        public DerivedKeys _derivedKeysDev;
        public DerivedKeys _derivedKeysProd;
        public DeviceKeys _deviceKeys;
        public RsaSigningKeys _rsaSigningKeysDev;
        public RsaSigningKeys _rsaSigningKeysProd;
        public RsaKeys _rsaKeys;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RootKeys
    {
        // Mariko keys. The AES class keys are currently unused.
        public AesKey MarikoKek;
        public AesKey MarikoBek;
        public Array12<AesKey> MarikoAesClassKeys;

        // The key blobs are technically derived from the encrypted key blobs and their keys,
        // however those keys are device-unique. The decrypted key blobs are basically the common root
        // keys used by pre-6.2.0 Erista.
        public Array32<KeyBlob> KeyBlobs;

        // Used by TSEC in >= 6.2.0 Erista firmware
        public Array32<AesKey> TsecAuthSignatures;
        public AesKey TsecRootKek;
        public AesKey Package1MacKek;
        public AesKey Package1Kek;

        // Derived by TSEC. This is the first public root key for >= 6.2.0 Erista
        public Array32<AesKey> TsecRootKeys;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KeySeeds
    {
        public Array32<AesKey> KeyBlobKeySources;
        public AesKey KeyBlobMacKeySource;
        public Array32<AesKey> MasterKekSources;
        public Array32<AesKey> MarikoMasterKekSources;
        public Array32<AesKey> MarikoMasterKekSources_dev;
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

    /// <summary>
    /// Holds keys that are stored directly in Horizon programs.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct StoredKeys
    {
        public AesKey XciHeaderKey;
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
        public AesKey ETicketRsaKek;
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

    [StructLayout(LayoutKind.Sequential)]
    public struct RsaSigningKeys
    {
        public Array2<RsaKey> NcaHeaderSigningKeys;
        public Array2<RsaKey> AcidSigningKeys;
        public RsaKey Package2SigningKey;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RsaKeys
    {
        public RsaFullKey BetaNca0KeyAreaKey;
    }
}
