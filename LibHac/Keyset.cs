using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LibHac.Streams;

namespace LibHac
{
    public class Keyset
    {
        public byte[][] KeyblobKeys { get; } = Util.CreateJaggedArray<byte[][]>(0x20, 0x10);
        public byte[][] KeyblobMacKeys { get; } = Util.CreateJaggedArray<byte[][]>(0x20, 0x10);
        public byte[][] EncryptedKeyblobs { get; } = Util.CreateJaggedArray<byte[][]>(0x20, 0xB0);
        public byte[][] Keyblobs { get; } = Util.CreateJaggedArray<byte[][]>(0x20, 0x90);
        public byte[][] KeyblobKeySources { get; } = Util.CreateJaggedArray<byte[][]>(0x20, 0x10);
        public byte[] KeyblobMacKeySource { get; } = new byte[0x10];
        public byte[] MasterKeySource { get; } = new byte[0x10];
        public byte[][] MasterKeys { get; } = Util.CreateJaggedArray<byte[][]>(0x20, 0x10);
        public byte[][] Package1Keys { get; } = Util.CreateJaggedArray<byte[][]>(0x20, 0x10);
        public byte[][] Package2Keys { get; } = Util.CreateJaggedArray<byte[][]>(0x20, 0x10);
        public byte[] Package2KeySource { get; } = new byte[0x10];
        public byte[] AesKekGenerationSource { get; } = new byte[0x10];
        public byte[] AesKeyGenerationSource { get; } = new byte[0x10];
        public byte[] KeyAreaKeyApplicationSource { get; } = new byte[0x10];
        public byte[] KeyAreaKeyOceanSource { get; } = new byte[0x10];
        public byte[] KeyAreaKeySystemSource { get; } = new byte[0x10];
        public byte[] SaveMacKekSource { get; } = new byte[0x10];
        public byte[] SaveMacKeySource { get; } = new byte[0x10];
        public byte[] TitlekekSource { get; } = new byte[0x10];
        public byte[] HeaderKekSource { get; } = new byte[0x10];
        public byte[] SdCardKekSource { get; } = new byte[0x10];
        public byte[][] SdCardKeySources { get; } = Util.CreateJaggedArray<byte[][]>(2, 0x20);
        public byte[][] SdCardKeySourcesSpecific { get; } = Util.CreateJaggedArray<byte[][]>(2, 0x20);
        public byte[] HeaderKeySource { get; } = new byte[0x20];
        public byte[] HeaderKey { get; } = new byte[0x20];
        public byte[] XciHeaderKey { get; } = new byte[0x10];
        public byte[][] Titlekeks { get; } = Util.CreateJaggedArray<byte[][]>(0x20, 0x10);
        public byte[][][] KeyAreaKeys { get; } = Util.CreateJaggedArray<byte[][][]>(0x20, 3, 0x10);
        public byte[] SaveMacKey { get; } = new byte[0x10];
        public byte[][] SdCardKeys { get; } = Util.CreateJaggedArray<byte[][]>(2, 0x20);
        public byte[] NcaHdrFixedKeyModulus { get; } = new byte[0x100];
        public byte[] AcidFixedKeyModulus { get; } = new byte[0x100];
        public byte[] Package2FixedKeyModulus { get; } = new byte[0x100];
        public byte[] EticketRsaKek { get; } = new byte[0x10];
        public byte[] RetailSpecificAesKeySource { get; } = new byte[0x10];
        public byte[] PerConsoleKeySource { get; } = new byte[0x10];
        public byte[] BisKekSource { get; } = new byte[0x10];
        public byte[][] BisKeySource { get; } = Util.CreateJaggedArray<byte[][]>(3, 0x20);

        public byte[] SecureBootKey { get; } = new byte[0x10];
        public byte[] TsecKey { get; } = new byte[0x10];
        public byte[] DeviceKey { get; } = new byte[0x10];
        public byte[][] BisKeys { get; } = Util.CreateJaggedArray<byte[][]>(4, 0x20);
        public byte[] SdSeed { get; } = new byte[0x10];

        public RSAParameters EticketExtKeyRsa { get; set; }

        public Dictionary<byte[], byte[]> TitleKeys { get; } = new Dictionary<byte[], byte[]>(new ByteArray128BitComparer());

        public void SetSdSeed(byte[] sdseed)
        {
            Array.Copy(sdseed, SdSeed, SdSeed.Length);
            DeriveKeys();
        }

        public void DeriveKeys(IProgressReport logger = null)
        {
            DeriveKeyblobKeys();
            DecryptKeyblobs(logger);
            ReadKeyblobs();

            DerivePerConsoleKeys();
            DerivePerFirmwareKeys();
            DeriveNcaHeaderKey();
            DeriveSdCardKeys();
        }

        private void DeriveKeyblobKeys()
        {
            if (SecureBootKey.IsEmpty() || TsecKey.IsEmpty()) return;

            bool haveKeyblobMacKeySource = !MasterKeySource.IsEmpty();
            var temp = new byte[0x10];

            for (int i = 0; i < 0x20; i++)
            {
                if (KeyblobKeySources[i].IsEmpty()) continue;

                Crypto.DecryptEcb(TsecKey, KeyblobKeySources[i], temp, 0x10);
                Crypto.DecryptEcb(SecureBootKey, temp, KeyblobKeys[i], 0x10);

                if (!haveKeyblobMacKeySource) continue;

                Crypto.DecryptEcb(KeyblobKeys[i], KeyblobMacKeySource, KeyblobMacKeys[i], 0x10);
            }
        }

        private void DecryptKeyblobs(IProgressReport logger = null)
        {
            var cmac = new byte[0x10];
            var expectedCmac = new byte[0x10];
            var counter = new byte[0x10];

            for (int i = 0; i < 0x20; i++)
            {
                if (KeyblobKeys[i].IsEmpty() || KeyblobMacKeys[i].IsEmpty() || EncryptedKeyblobs[i].IsEmpty())
                {
                    continue;
                }

                Array.Copy(EncryptedKeyblobs[i], expectedCmac, 0x10);
                Crypto.CalculateAesCmac(KeyblobMacKeys[i], EncryptedKeyblobs[i], 0x10, cmac, 0, 0xa0);

                if (!Util.ArraysEqual(cmac, expectedCmac))
                {
                    logger?.LogMessage($"Warning: Keyblob MAC {i:x2} is invalid. Are SBK/TSEC key correct?");
                }

                Array.Copy(EncryptedKeyblobs[i], 0x10, counter, 0, 0x10);

                using (var keyblobDec = new RandomAccessSectorStream(new Aes128CtrStream(
                    new MemoryStream(EncryptedKeyblobs[i], 0x20, Keyblobs[i].Length), KeyblobKeys[i], counter)))
                {
                    keyblobDec.Read(Keyblobs[i], 0, Keyblobs[i].Length);
                }
            }
        }

        private void ReadKeyblobs()
        {
            var masterKek = new byte[0x10];

            bool haveMasterKeySource = !MasterKeySource.IsEmpty();

            for (int i = 0; i < 0x20; i++)
            {
                if (Keyblobs[i].IsEmpty()) continue;

                Array.Copy(Keyblobs[i], 0x80, Package1Keys[i], 0, 0x10);

                if (!haveMasterKeySource) continue;

                Array.Copy(Keyblobs[i], masterKek, 0x10);

                Crypto.DecryptEcb(masterKek, MasterKeySource, MasterKeys[i], 0x10);
            }
        }

        private void DerivePerConsoleKeys()
        {
            var kek = new byte[0x10];

            // Derive the device key
            if (!PerConsoleKeySource.IsEmpty() && !KeyblobKeys[0].IsEmpty())
            {
                Crypto.DecryptEcb(KeyblobKeys[0], PerConsoleKeySource, DeviceKey, 0x10);
            }

            // Derive save key
            if (!SaveMacKekSource.IsEmpty() && !SaveMacKeySource.IsEmpty() && !DeviceKey.IsEmpty())
            {
                Crypto.GenerateKek(DeviceKey, SaveMacKekSource, kek, AesKekGenerationSource, null);
                Crypto.DecryptEcb(kek, SaveMacKeySource, SaveMacKey, 0x10);
            }

            // Derive BIS keys
            if (DeviceKey.IsEmpty()
                || BisKeySource[0].IsEmpty()
                || BisKeySource[1].IsEmpty()
                || BisKeySource[2].IsEmpty()
                || BisKekSource.IsEmpty()
                || AesKekGenerationSource.IsEmpty()
                || AesKeyGenerationSource.IsEmpty()
                || RetailSpecificAesKeySource.IsEmpty())
            {
                return;
            }

            Crypto.DecryptEcb(DeviceKey, RetailSpecificAesKeySource, kek, 0x10);
            Crypto.DecryptEcb(kek, BisKeySource[0], BisKeys[0], 0x20);

            Crypto.GenerateKek(DeviceKey, BisKekSource, kek, AesKekGenerationSource, AesKeyGenerationSource);

            Crypto.DecryptEcb(kek, BisKeySource[1], BisKeys[1], 0x20);
            Crypto.DecryptEcb(kek, BisKeySource[2], BisKeys[2], 0x20);

            // BIS keys 2 and 3 are the same
            Array.Copy(BisKeys[2], BisKeys[3], 0x20);
        }

        private void DerivePerFirmwareKeys()
        {
            bool haveKakSource0 = !KeyAreaKeyApplicationSource.IsEmpty();
            bool haveKakSource1 = !KeyAreaKeyOceanSource.IsEmpty();
            bool haveKakSource2 = !KeyAreaKeySystemSource.IsEmpty();
            bool haveTitleKekSource = !TitlekekSource.IsEmpty();
            bool havePackage2KeySource = !Package2KeySource.IsEmpty();

            for (int i = 0; i < 0x20; i++)
            {
                if (MasterKeys[i].IsEmpty())
                {
                    continue;
                }

                if (haveKakSource0)
                {
                    Crypto.GenerateKek(MasterKeys[i], KeyAreaKeyApplicationSource, KeyAreaKeys[i][0],
                        AesKekGenerationSource, AesKeyGenerationSource);
                }

                if (haveKakSource1)
                {
                    Crypto.GenerateKek(MasterKeys[i], KeyAreaKeyOceanSource, KeyAreaKeys[i][1],
                        AesKekGenerationSource, AesKeyGenerationSource);
                }

                if (haveKakSource2)
                {
                    Crypto.GenerateKek(MasterKeys[i], KeyAreaKeySystemSource, KeyAreaKeys[i][2],
                        AesKekGenerationSource, AesKeyGenerationSource);
                }

                if (haveTitleKekSource)
                {
                    Crypto.DecryptEcb(MasterKeys[i], TitlekekSource, Titlekeks[i], 0x10);
                }

                if (havePackage2KeySource)
                {
                    Crypto.DecryptEcb(MasterKeys[i], Package2KeySource, Package2Keys[i], 0x10);
                }
            }
        }

        private void DeriveNcaHeaderKey()
        {
            if (HeaderKekSource.IsEmpty() || HeaderKeySource.IsEmpty() || MasterKeys[0].IsEmpty()) return;

            var headerKek = new byte[0x10];

            Crypto.GenerateKek(MasterKeys[0], HeaderKekSource, headerKek, AesKekGenerationSource,
                AesKeyGenerationSource);
            Crypto.DecryptEcb(headerKek, HeaderKeySource, HeaderKey, 0x20);
        }

        public void DeriveSdCardKeys()
        {
            var sdKek = new byte[0x10];
            Crypto.GenerateKek(MasterKeys[0], SdCardKekSource, sdKek, AesKekGenerationSource, AesKeyGenerationSource);

            for (int k = 0; k < SdCardKeySources.Length; k++)
            {
                for (int i = 0; i < 0x20; i++)
                {
                    SdCardKeySourcesSpecific[k][i] = (byte)(SdCardKeySources[k][i] ^ SdSeed[i & 0xF]);
                }
            }

            for (int k = 0; k < SdCardKeySourcesSpecific.Length; k++)
            {
                Crypto.DecryptEcb(sdKek, SdCardKeySourcesSpecific[k], SdCardKeys[k], 0x20);
            }
        }

        internal static readonly string[] KakNames = {"application", "ocean", "system"};
    }

    public static class ExternalKeys
    {
        private const int TitleKeySize = 0x10;

        public static readonly Dictionary<string, KeyValue> CommonKeyDict;
        public static readonly Dictionary<string, KeyValue> UniqueKeyDict;
        public static readonly Dictionary<string, KeyValue> AllKeyDict;

        static ExternalKeys()
        {
            List<KeyValue> commonKeys = CreateCommonKeyList();
            List<KeyValue> uniqueKeys = CreateUniqueKeyList();

            CommonKeyDict = commonKeys.ToDictionary(k => k.Name, k => k);
            UniqueKeyDict = uniqueKeys.ToDictionary(k => k.Name, k => k);
            AllKeyDict = uniqueKeys.Concat(commonKeys).ToDictionary(k => k.Name, k => k);
        }

        public static Keyset ReadKeyFile(string filename, string titleKeysFilename = null, string consoleKeysFilename = null, IProgressReport logger = null)
        {
            var keyset = new Keyset();

            if (filename != null) ReadMainKeys(keyset, filename, AllKeyDict, logger);
            if (consoleKeysFilename != null) ReadMainKeys(keyset, consoleKeysFilename, AllKeyDict, logger);
            if (titleKeysFilename != null) ReadTitleKeys(keyset, titleKeysFilename, logger);
            keyset.DeriveKeys(logger);

            return keyset;
        }

        public static void LoadConsoleKeys(this Keyset keyset, string filename, IProgressReport logger = null)
        {
            foreach (KeyValue key in UniqueKeyDict.Values)
            {
                byte[] keyBytes = key.GetKey(keyset);
                Array.Clear(keyBytes, 0, keyBytes.Length);
            }

            ReadMainKeys(keyset, filename, UniqueKeyDict, logger);
            keyset.DeriveKeys();
        }

        private static void ReadMainKeys(Keyset keyset, string filename, Dictionary<string, KeyValue> keyDict, IProgressReport logger = null)
        {
            if (filename == null) return;

            using (var reader = new StreamReader(new FileStream(filename, FileMode.Open, FileAccess.Read)))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] a = line.Split(',', '=');
                    if (a.Length != 2) continue;

                    string key = a[0].Trim();
                    string valueStr = a[1].Trim();

                    if (!keyDict.TryGetValue(key, out KeyValue kv))
                    {
                        logger?.LogMessage($"Failed to match key {key}");
                        continue;
                    }

                    byte[] value = valueStr.ToBytes();
                    if (value.Length != kv.Size)
                    {
                        logger?.LogMessage($"Key {key} had incorrect size {value.Length}. (Expected {kv.Size})");
                        continue;
                    }

                    byte[] dest = kv.GetKey(keyset);
                    Array.Copy(value, dest, value.Length);
                }
            }
        }

        private static void ReadTitleKeys(Keyset keyset, string filename, IProgressReport progress = null)
        {
            if (filename == null) return;

            using (var reader = new StreamReader(new FileStream(filename, FileMode.Open, FileAccess.Read)))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] splitLine;

                    // Some people use pipes as delimiters
                    if (line.Contains('|'))
                    {
                        splitLine = line.Split('|');
                    }
                    else
                    {
                        splitLine = line.Split(',', '=');
                    }

                    if (splitLine.Length < 2) continue;

                    if (!splitLine[0].Trim().TryToBytes(out byte[] rightsId))
                    {
                        progress?.LogMessage($"Invalid rights ID \"{splitLine[0].Trim()}\" in title key file");
                        continue;
                    }

                    if (!splitLine[1].Trim().TryToBytes(out byte[] titleKey))
                    {
                        progress?.LogMessage($"Invalid title key \"{splitLine[1].Trim()}\" in title key file");
                        continue;
                    }

                    if (rightsId.Length != TitleKeySize)
                    {
                        progress?.LogMessage($"Rights ID {rightsId.ToHexString()} had incorrect size {rightsId.Length}. (Expected {TitleKeySize})");
                        continue;
                    }

                    if (titleKey.Length != TitleKeySize)
                    {
                        progress?.LogMessage($"Title key {titleKey.ToHexString()} had incorrect size {titleKey.Length}. (Expected {TitleKeySize})");
                        continue;
                    }

                    keyset.TitleKeys[rightsId] = titleKey;
                }
            }
        }

        public static string PrintKeys(Keyset keyset, Dictionary<string, KeyValue> dict)
        {
            if (dict.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            int maxNameLength = dict.Values.Max(x => x.Name.Length);

            foreach (KeyValue keySlot in dict.Values.OrderBy(x => x.Name))
            {
                byte[] key = keySlot.GetKey(keyset);
                if (key.IsEmpty()) continue;

                string line = $"{keySlot.Name.PadRight(maxNameLength)} = {key.ToHexString()}";
                sb.AppendLine(line);
            }

            return sb.ToString();
        }

        public static string PrintCommonKeys(Keyset keyset)
        {
            return PrintKeys(keyset, CommonKeyDict);
        }

        public static string PrintUniqueKeys(Keyset keyset)
        {
            return PrintKeys(keyset, UniqueKeyDict);
        }

        public static string PrintAllKeys(Keyset keyset)
        {
            return PrintKeys(keyset, AllKeyDict);
        }

        public static string PrintTitleKeys(Keyset keyset)
        {
            var sb = new StringBuilder();

            foreach (KeyValuePair<byte[], byte[]> kv in keyset.TitleKeys)
            {
                string line = $"{kv.Key.ToHexString()} = {kv.Value.ToHexString()}";
                sb.AppendLine(line);
            }

            return sb.ToString();
        }

        private static List<KeyValue> CreateCommonKeyList()
        {
            var keys = new List<KeyValue>
            {
                new KeyValue("aes_kek_generation_source", 0x10, set => set.AesKekGenerationSource),
                new KeyValue("aes_key_generation_source", 0x10, set => set.AesKeyGenerationSource),
                new KeyValue("key_area_key_application_source", 0x10, set => set.KeyAreaKeyApplicationSource),
                new KeyValue("key_area_key_ocean_source", 0x10, set => set.KeyAreaKeyOceanSource),
                new KeyValue("key_area_key_system_source", 0x10, set => set.KeyAreaKeySystemSource),
                new KeyValue("titlekek_source", 0x10, set => set.TitlekekSource),
                new KeyValue("header_kek_source", 0x10, set => set.HeaderKekSource),
                new KeyValue("header_key_source", 0x20, set => set.HeaderKeySource),
                new KeyValue("header_key", 0x20, set => set.HeaderKey),
                new KeyValue("xci_header_key", 0x10, set => set.XciHeaderKey),
                new KeyValue("package2_key_source", 0x10, set => set.Package2KeySource),
                new KeyValue("sd_card_kek_source", 0x10, set => set.SdCardKekSource),
                new KeyValue("sd_card_nca_key_source", 0x20, set => set.SdCardKeySources[1]),
                new KeyValue("sd_card_save_key_source", 0x20, set => set.SdCardKeySources[0]),
                new KeyValue("master_key_source", 0x10, set => set.MasterKeySource),
                new KeyValue("keyblob_mac_key_source", 0x10, set => set.KeyblobMacKeySource),
                new KeyValue("eticket_rsa_kek", 0x10, set => set.EticketRsaKek),
                new KeyValue("retail_specific_aes_key_source", 0x10, set => set.RetailSpecificAesKeySource),
                new KeyValue("per_console_key_source", 0x10, set => set.PerConsoleKeySource),
                new KeyValue("bis_kek_source", 0x10, set => set.BisKekSource),
                new KeyValue("save_mac_kek_source", 0x10, set => set.SaveMacKekSource),
                new KeyValue("save_mac_key_source", 0x10, set => set.SaveMacKeySource),
                new KeyValue("save_mac_key", 0x10, set => set.SaveMacKey)
            };

            for (int slot = 0; slot < 0x20; slot++)
            {
                int i = slot;
                keys.Add(new KeyValue($"keyblob_key_source_{i:x2}", 0x10, set => set.KeyblobKeySources[i]));
                keys.Add(new KeyValue($"keyblob_{i:x2}", 0x90, set => set.Keyblobs[i]));
                keys.Add(new KeyValue($"master_key_{i:x2}", 0x10, set => set.MasterKeys[i]));
                keys.Add(new KeyValue($"package1_key_{i:x2}", 0x10, set => set.Package1Keys[i]));
                keys.Add(new KeyValue($"package2_key_{i:x2}", 0x10, set => set.Package2Keys[i]));
                keys.Add(new KeyValue($"titlekek_{i:x2}", 0x10, set => set.Titlekeks[i]));
                keys.Add(new KeyValue($"key_area_key_application_{i:x2}", 0x10, set => set.KeyAreaKeys[i][0]));
                keys.Add(new KeyValue($"key_area_key_ocean_{i:x2}", 0x10, set => set.KeyAreaKeys[i][1]));
                keys.Add(new KeyValue($"key_area_key_system_{i:x2}", 0x10, set => set.KeyAreaKeys[i][2]));
            }

            for (int slot = 0; slot < 3; slot++)
            {
                int i = slot;
                keys.Add(new KeyValue($"bis_key_source_{i:x2}", 0x20, set => set.BisKeySource[i]));
            }

            return keys;
        }

        private static List<KeyValue> CreateUniqueKeyList()
        {
            var keys = new List<KeyValue>
            {
                new KeyValue("secure_boot_key", 0x10, set => set.SecureBootKey),
                new KeyValue("tsec_key", 0x10, set => set.TsecKey),
                new KeyValue("device_key", 0x10, set => set.DeviceKey),
                new KeyValue("sd_seed", 0x10, set => set.SdSeed)
            };

            for (int slot = 0; slot < 0x20; slot++)
            {
                int i = slot;
                keys.Add(new KeyValue($"keyblob_key_{i:x2}", 0x10, set => set.KeyblobKeys[i]));
                keys.Add(new KeyValue($"keyblob_mac_key_{i:x2}", 0x10, set => set.KeyblobMacKeys[i]));
                keys.Add(new KeyValue($"encrypted_keyblob_{i:x2}", 0xB0, set => set.EncryptedKeyblobs[i]));
            }

            for (int slot = 0; slot < 4; slot++)
            {
                int i = slot;
                keys.Add(new KeyValue($"bis_key_{i:x2}", 0x20, set => set.BisKeys[i]));
            }

            return keys;
        }

        public class KeyValue
        {
            public readonly string Name;
            public readonly int Size;
            public readonly Func<Keyset, byte[]> GetKey;

            public KeyValue(string name, int size, Func<Keyset, byte[]> retrieveFunc)
            {
                Name = name;
                Size = size;
                GetKey = retrieveFunc;
            }
        }
    }

    public enum KeyType
    {
        None,
        Common,
        Unique,
        Title
    }
}
