using System;
using System.Runtime.InteropServices;
using LibHac.Crypto;

namespace LibHac.Common.Keys
{
    internal static class KeyDerivation
    {
        public static void DeriveAllKeys(KeySet keySet, IProgressReport logger = null)
        {
            DeriveKeyBlobKeys(keySet);
            DecryptKeyBlobs(keySet, logger);
            ReadKeyBlobs(keySet);

            Derive620Keys(keySet);
            Derive620MasterKeks(keySet);
            DeriveMarikoMasterKeks(keySet);
            DeriveMasterKeys(keySet);
            PopulateOldMasterKeys(keySet);

            DerivePerConsoleKeys(keySet);
            DerivePerGenerationKeys(keySet);
            DeriveNcaHeaderKey(keySet);
            DeriveSdCardKeys(keySet);
        }

        private static void DeriveKeyBlobKeys(KeySet s)
        {
            if (s.SecureBootKey.IsZeros() || s.TsecKey.IsZeros()) return;

            bool haveKeyBlobMacKeySource = !s.MasterKeySource.IsZeros();
            var temp = new AesKey();

            for (int i = 0; i < KeySet.UsedKeyBlobCount; i++)
            {
                if (s.KeyBlobKeySources[i].IsZeros()) continue;

                Aes.DecryptEcb128(s.KeyBlobKeySources[i], temp, s.TsecKey);
                Aes.DecryptEcb128(temp, s.KeyBlobKeys[i], s.SecureBootKey);

                if (!haveKeyBlobMacKeySource) continue;

                Aes.DecryptEcb128(s.KeyBlobMacKeySource, s.KeyBlobMacKeys[i], s.KeyBlobKeys[i]);
            }
        }

        private static void DecryptKeyBlobs(KeySet s, IProgressReport logger = null)
        {
            var cmac = new AesCmac();

            for (int i = 0; i < KeySet.UsedKeyBlobCount; i++)
            {
                if (s.KeyBlobKeys[i].IsZeros() || s.KeyBlobMacKeys[i].IsZeros() || s.EncryptedKeyBlobs[i].IsZeros())
                {
                    continue;
                }

                Aes.CalculateCmac(cmac, s.EncryptedKeyBlobs[i].Bytes.Slice(0x10, 0xA0), s.KeyBlobMacKeys[i]);

                if (!Utilities.SpansEqual<byte>(cmac, s.EncryptedKeyBlobs[i].Cmac))
                {
                    logger?.LogMessage($"Warning: Keyblob MAC {i:x2} is invalid. Are SBK/TSEC key correct?");
                }

                Aes.DecryptCtr128(s.EncryptedKeyBlobs[i].Bytes.Slice(0x20), s.KeyBlobs[i].Bytes, s.KeyBlobKeys[i],
                    s.EncryptedKeyBlobs[i].Counter);
            }
        }

        private static void ReadKeyBlobs(KeySet s)
        {
            for (int i = 0; i < KeySet.UsedKeyBlobCount; i++)
            {
                if (s.KeyBlobs[i].IsZeros()) continue;

                s.MasterKeks[i] = s.KeyBlobs[i].MasterKek;
                s.Package1Keys[i] = s.KeyBlobs[i].Package1Key;
            }
        }

        private static void Derive620Keys(KeySet s)
        {
            bool haveTsecRootKek = !s.TsecRootKek.IsZeros();
            bool havePackage1MacKek = !s.Package1MacKek.IsZeros();
            bool havePackage1Kek = !s.Package1Kek.IsZeros();

            for (int i = KeySet.UsedKeyBlobCount; i < KeySet.KeyRevisionCount; i++)
            {
                if (s.TsecAuthSignatures[i - KeySet.UsedKeyBlobCount].IsZeros())
                    continue;

                if (haveTsecRootKek)
                {
                    Aes.EncryptEcb128(s.TsecAuthSignatures[i - KeySet.UsedKeyBlobCount],
                        s.TsecRootKeys[i - KeySet.UsedKeyBlobCount], s.TsecRootKek);
                }

                if (havePackage1MacKek)
                {
                    Aes.EncryptEcb128(s.TsecAuthSignatures[i - KeySet.UsedKeyBlobCount], s.Package1MacKeys[i],
                        s.Package1MacKek);
                }

                if (havePackage1Kek)
                {
                    Aes.EncryptEcb128(s.TsecAuthSignatures[i - KeySet.UsedKeyBlobCount], s.Package1Keys[i], s.Package1Kek);
                }
            }
        }

        private static void Derive620MasterKeks(KeySet s)
        {
            for (int i = KeySet.UsedKeyBlobCount; i < KeySet.KeyRevisionCount; i++)
            {
                // Key revisions >= 8 all use the same TSEC root key
                int tsecRootKeyIndex = Math.Min(i, 8) - KeySet.UsedKeyBlobCount;
                if (s.TsecRootKeys[tsecRootKeyIndex].IsZeros() || s.MasterKekSources[i].IsZeros()) continue;

                Aes.DecryptEcb128(s.MasterKekSources[i], s.MasterKeks[i], s.TsecRootKeys[tsecRootKeyIndex]);
            }
        }

        private static void DeriveMarikoMasterKeks(KeySet s)
        {
            if (s.MarikoKek.IsZeros()) return;

            for (int i = 0; i < KeySet.KeyRevisionCount; i++)
            {
                if (s.MarikoMasterKekSources[i].IsZeros()) continue;

                Aes.DecryptEcb128(s.MarikoMasterKekSources[i], s.MasterKeks[i], s.MarikoKek);
            }
        }

        private static void DeriveMasterKeys(KeySet s)
        {
            if (s.MasterKeySource.IsZeros()) return;

            for (int i = 0; i < KeySet.KeyRevisionCount; i++)
            {
                if (s.MasterKeks[i].IsZeros()) continue;

                Aes.DecryptEcb128(s.MasterKeySource, s.MasterKeys[i], s.MasterKeks[i]);
            }
        }

        private static void PopulateOldMasterKeys(KeySet s)
        {
            ReadOnlySpan<AesKey> keyVectors = MasterKeyVectors(s);

            // Find the newest master key we have
            int newestMasterKey = -1;

            for (int i = keyVectors.Length - 1; i >= 0; i--)
            {
                if (!s.MasterKeys[i].IsZeros())
                {
                    newestMasterKey = i;
                    break;
                }
            }

            if (newestMasterKey == -1)
                return;

            // Don't populate old master keys unless the newest master key is valid
            if (!TestKeyGeneration(s, newestMasterKey))
                return;

            // Decrypt all previous master keys
            for (int i = newestMasterKey; i > 0; i--)
            {
                Aes.DecryptEcb128(keyVectors[i], s.MasterKeys[i - 1], s.MasterKeys[i]);
            }
        }

        /// <summary>
        /// Check if the master key of the specified generation is correct.
        /// </summary>
        /// <param name="s">The <see cref="KeySet"/> to test.</param>
        /// <param name="generation">The generation to test.</param>
        /// <returns><see langword="true"/> if the key is correct.</returns>
        private static bool TestKeyGeneration(KeySet s, int generation)
        {
            ReadOnlySpan<AesKey> keyVectors = MasterKeyVectors(s);

            // Decrypt the vector chain until we get Master Key 0
            AesKey key = s.MasterKeys[generation];

            for (int i = generation; i > 0; i--)
            {
                Aes.DecryptEcb128(keyVectors[i], key, key);
            }

            // Decrypt the zeros with Master Key 0
            Aes.DecryptEcb128(keyVectors[0], key, key);

            // If we don't get zeros, MasterKeys[generation] is incorrect
            return key.IsZeros();
        }

        private static ReadOnlySpan<AesKey> MasterKeyVectors(KeySet s) =>
            MemoryMarshal.Cast<byte, AesKey>(s.CurrentMode == KeySet.Mode.Dev
                ? MasterKeyVectorsDev
                : MasterKeyVectorsProd);

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
            0x6C, 0x2E, 0xCD, 0xB3, 0x34, 0x61, 0x77, 0xF5, 0xF9, 0xB1, 0xDD, 0x61, 0x98, 0x19, 0x3E, 0xD4, // Master key 09 encrypted with Master key 0A.
            0x21, 0x88, 0x6B, 0x10, 0x9E, 0x83, 0xD6, 0x52, 0xAB, 0x08, 0xDB, 0x6D, 0x39, 0xFF, 0x1C, 0x9C  // Master key 0A encrypted with Master key 0B.
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
            0xB8, 0x96, 0x9E, 0x4A, 0x00, 0x0D, 0xD6, 0x28, 0xB3, 0xD1, 0xDB, 0x68, 0x5F, 0xFB, 0xE1, 0x2A, // Master key 09 encrypted with Master key 0A.
            0xC1, 0x8D, 0x16, 0xBB, 0x2A, 0xE4, 0x1D, 0xD4, 0xC2, 0xC1, 0xB6, 0x40, 0x94, 0x35, 0x63, 0x98  // Master key 0A encrypted with Master key 0B.
        };

        private static void DerivePerConsoleKeys(KeySet s)
        {
            // Todo: Dev and newer key generations
            var kek = new AesKey();

            // Derive the device key
            if (!s.PerConsoleKeySource.IsZeros() && !s.KeyBlobKeys[0].IsZeros())
            {
                Aes.DecryptEcb128(s.PerConsoleKeySource, s.DeviceKey, s.KeyBlobKeys[0]);
            }

            // Derive device-unique save keys
            for (int i = 0; i < s.DeviceUniqueSaveMacKeySources.Length; i++)
            {
                if (!s.DeviceUniqueSaveMacKekSource.IsZeros() && !s.DeviceUniqueSaveMacKeySources[i].IsZeros() &&
                    !s.DeviceKey.IsZeros())
                {
                    GenerateKek(s.DeviceKey, s.DeviceUniqueSaveMacKekSource, kek, s.AesKekGenerationSource, null);
                    Aes.DecryptEcb128(s.DeviceUniqueSaveMacKeySources[i], s.DeviceUniqueSaveMacKeys[i], kek);
                }
            }

            // Derive BIS keys
            if (s.DeviceKey.IsZeros()
                || s.BisKekSource.IsZeros()
                || s.AesKekGenerationSource.IsZeros()
                || s.AesKeyGenerationSource.IsZeros()
                || s.RetailSpecificAesKeySource.IsZeros())
            {
                return;
            }

            // If the user doesn't provide bis_key_source_03 we can assume it's the same as bis_key_source_02
            if (s.BisKeySources[3].IsZeros() && !s.BisKeySources[2].IsZeros())
            {
                s.BisKeySources[3] = s.BisKeySources[2];
            }

            Aes.DecryptEcb128(s.RetailSpecificAesKeySource, kek, s.DeviceKey);
            if (!s.BisKeySources[0].IsZeros()) Aes.DecryptEcb128(s.BisKeySources[0], s.BisKeys[0], kek);

            GenerateKek(s.DeviceKey, s.BisKekSource, kek, s.AesKekGenerationSource, s.AesKeyGenerationSource);

            for (int i = 1; i < 4; i++)
            {
                if (!s.BisKeySources[i].IsZeros())
                    Aes.DecryptEcb128(s.BisKeySources[i], s.BisKeys[i], kek);
            }
        }

        private static void DerivePerGenerationKeys(KeySet s)
        {
            bool haveKakSource0 = !s.KeyAreaKeyApplicationSource.IsZeros();
            bool haveKakSource1 = !s.KeyAreaKeyOceanSource.IsZeros();
            bool haveKakSource2 = !s.KeyAreaKeySystemSource.IsZeros();
            bool haveTitleKekSource = !s.TitleKekSource.IsZeros();
            bool havePackage2KeySource = !s.Package2KeySource.IsZeros();

            for (int i = 0; i < KeySet.KeyRevisionCount; i++)
            {
                if (s.MasterKeys[i].IsZeros())
                {
                    continue;
                }

                if (haveKakSource0)
                {
                    GenerateKek(s.MasterKeys[i], s.KeyAreaKeyApplicationSource, s.KeyAreaKeys[i][0],
                        s.AesKekGenerationSource, s.AesKeyGenerationSource);
                }

                if (haveKakSource1)
                {
                    GenerateKek(s.MasterKeys[i], s.KeyAreaKeyOceanSource, s.KeyAreaKeys[i][1], s.AesKekGenerationSource,
                        s.AesKeyGenerationSource);
                }

                if (haveKakSource2)
                {
                    GenerateKek(s.MasterKeys[i], s.KeyAreaKeySystemSource, s.KeyAreaKeys[i][2],
                        s.AesKekGenerationSource, s.AesKeyGenerationSource);
                }

                if (haveTitleKekSource)
                {
                    Aes.DecryptEcb128(s.TitleKekSource, s.TitleKeks[i], s.MasterKeys[i]);
                }

                if (havePackage2KeySource)
                {
                    Aes.DecryptEcb128(s.Package2KeySource, s.Package2Keys[i], s.MasterKeys[i]);
                }
            }
        }

        private static void DeriveNcaHeaderKey(KeySet s)
        {
            if (s.HeaderKekSource.IsZeros() || s.HeaderKeySource.IsZeros() || s.MasterKeys[0].IsZeros()) return;

            var headerKek = new AesKey();

            GenerateKek(s.MasterKeys[0], s.HeaderKekSource, headerKek, s.AesKekGenerationSource,
                s.AesKeyGenerationSource);
            Aes.DecryptEcb128(s.HeaderKeySource, s.HeaderKey, headerKek);
        }

        public static void DeriveSdCardKeys(KeySet s)
        {
            var sdKek = new AesKey();
            var tempKey = new AesXtsKey();
            GenerateKek(s.MasterKeys[0], s.SdCardKekSource, sdKek, s.AesKekGenerationSource, s.AesKeyGenerationSource);

            for (int k = 0; k < KeySet.SdCardKeyIdCount; k++)
            {
                for (int i = 0; i < 4; i++)
                {
                    tempKey.Data64[i] = s.SdCardKeySources[k].Data64[i] ^ s.SdCardEncryptionSeed.Data64[i & 1];
                }

                tempKey.Data64[0] = s.SdCardKeySources[k].Data64[0] ^ s.SdCardEncryptionSeed.Data64[0];
                tempKey.Data64[1] = s.SdCardKeySources[k].Data64[1] ^ s.SdCardEncryptionSeed.Data64[1];
                tempKey.Data64[2] = s.SdCardKeySources[k].Data64[2] ^ s.SdCardEncryptionSeed.Data64[0];
                tempKey.Data64[3] = s.SdCardKeySources[k].Data64[3] ^ s.SdCardEncryptionSeed.Data64[1];

                Aes.DecryptEcb128(tempKey, s.SdCardEncryptionKeys[k], sdKek);
            }

            // Derive sd card save key
            if (!s.SeedUniqueSaveMacKekSource.IsZeros() && !s.SeedUniqueSaveMacKeySource.IsZeros())
            {
                var keySource = new AesKey();

                keySource.Data64[0] = s.SeedUniqueSaveMacKeySource.Data64[0] ^ s.SdCardEncryptionSeed.Data64[0];
                keySource.Data64[1] = s.SeedUniqueSaveMacKeySource.Data64[1] ^ s.SdCardEncryptionSeed.Data64[1];

                GenerateKek(s.MasterKeys[0], s.SeedUniqueSaveMacKekSource, sdKek, s.AesKekGenerationSource, null);
                Aes.DecryptEcb128(keySource, s.SeedUniqueSaveMacKey, sdKek);
            }
        }

        private static void GenerateKek(ReadOnlySpan<byte> key, ReadOnlySpan<byte> src, Span<byte> dest,
            ReadOnlySpan<byte> kekSeed, ReadOnlySpan<byte> keySeed)
        {
            var kek = new AesKey();
            var srcKek = new AesKey();

            Aes.DecryptEcb128(kekSeed, kek, key);
            Aes.DecryptEcb128(src, srcKek, kek);

            if (!keySeed.IsZeros())
            {
                Aes.DecryptEcb128(keySeed, dest, srcKek);
            }
            else
            {
                srcKek.Data.CopyTo(dest);
            }
        }
    }
}
