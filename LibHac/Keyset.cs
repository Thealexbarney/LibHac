// ReSharper disable InconsistentNaming

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
        public byte[][] keyblob_keys { get; set; } = Util.CreateJaggedArray<byte[][]>(0x20, 0x10);
        public byte[][] keyblob_mac_keys { get; set; } = Util.CreateJaggedArray<byte[][]>(0x20, 0x10);
        public byte[][] encrypted_keyblobs { get; set; } = Util.CreateJaggedArray<byte[][]>(0x20, 0xB0);
        public byte[][] keyblobs { get; set; } = Util.CreateJaggedArray<byte[][]>(0x20, 0x90);
        public byte[][] keyblob_key_sources { get; set; } = Util.CreateJaggedArray<byte[][]>(0x20, 0x10);
        public byte[] keyblob_mac_key_source { get; set; } = new byte[0x10];
        public byte[] master_key_source { get; set; } = new byte[0x10];
        public byte[][] master_keys { get; set; } = Util.CreateJaggedArray<byte[][]>(0x20, 0x10);
        public byte[][] package1_keys { get; set; } = Util.CreateJaggedArray<byte[][]>(0x20, 0x10);
        public byte[][] package2_keys { get; set; } = Util.CreateJaggedArray<byte[][]>(0x20, 0x10);
        public byte[] package2_key_source { get; set; } = new byte[0x10];
        public byte[] aes_kek_generation_source { get; set; } = new byte[0x10];
        public byte[] aes_key_generation_source { get; set; } = new byte[0x10];
        public byte[] key_area_key_application_source { get; set; } = new byte[0x10];
        public byte[] key_area_key_ocean_source { get; set; } = new byte[0x10];
        public byte[] key_area_key_system_source { get; set; } = new byte[0x10];
        public byte[] titlekek_source { get; set; } = new byte[0x10];
        public byte[] header_kek_source { get; set; } = new byte[0x10];
        public byte[] sd_card_kek_source { get; set; } = new byte[0x10];
        public byte[][] sd_card_key_sources { get; set; } = Util.CreateJaggedArray<byte[][]>(2, 0x20);
        public byte[][] sd_card_key_sources_specific { get; set; } = Util.CreateJaggedArray<byte[][]>(2, 0x20);
        public byte[] header_key_source { get; set; } = new byte[0x20];
        public byte[] header_key { get; set; } = new byte[0x20];
        public byte[] xci_header_key { get; set; } = new byte[0x10];
        public byte[][] titlekeks { get; set; } = Util.CreateJaggedArray<byte[][]>(0x20, 0x10);
        public byte[][][] key_area_keys { get; set; } = Util.CreateJaggedArray<byte[][][]>(0x20, 3, 0x10);
        public byte[][] sd_card_keys { get; set; } = Util.CreateJaggedArray<byte[][]>(2, 0x20);
        public byte[] nca_hdr_fixed_key_modulus { get; set; } = new byte[0x100];
        public byte[] acid_fixed_key_modulus { get; set; } = new byte[0x100];
        public byte[] package2_fixed_key_modulus { get; set; } = new byte[0x100];
        public byte[] eticket_rsa_kek { get; set; } = new byte[0x10];
        public byte[] retail_specific_aes_key_source { get; set; } = new byte[0x10];
        public byte[] per_console_key_source { get; set; } = new byte[0x10];
        public byte[] bis_kek_source { get; set; } = new byte[0x10];
        public byte[][] bis_key_source { get; set; } = Util.CreateJaggedArray<byte[][]>(3, 0x20);

        public byte[] secure_boot_key { get; set; } = new byte[0x10];
        public byte[] tsec_key { get; set; } = new byte[0x10];
        public byte[] device_key { get; set; } = new byte[0x10];
        public byte[][] bis_keys { get; set; } = Util.CreateJaggedArray<byte[][]>(4, 0x20);
        public byte[] sd_seed { get; set; } = new byte[0x10];
        public RSAParameters eticket_ext_key_rsa { get; set; }

        public Dictionary<byte[], byte[]> TitleKeys { get; } = new Dictionary<byte[], byte[]>(new ByteArray128BitComparer());

        public void SetSdSeed(byte[] sdseed)
        {
            Array.Copy(sdseed, sd_seed, sd_seed.Length);
            DeriveKeys();
        }

        internal void DeriveKeys(IProgressReport logger = null)
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
            if (secure_boot_key.IsEmpty() || tsec_key.IsEmpty()) return;

            bool haveKeyblobMacKeySource = !master_key_source.IsEmpty();
            var temp = new byte[0x10];

            for (int i = 0; i < 0x20; i++)
            {
                if (keyblob_key_sources[i].IsEmpty()) continue;

                Crypto.DecryptEcb(tsec_key, keyblob_key_sources[i], temp, 0x10);
                Crypto.DecryptEcb(secure_boot_key, temp, keyblob_keys[i], 0x10);

                if (!haveKeyblobMacKeySource) continue;

                Crypto.DecryptEcb(keyblob_keys[i], keyblob_mac_key_source, keyblob_mac_keys[i], 0x10);
            }
        }

        private void DecryptKeyblobs(IProgressReport logger = null)
        {
            var cmac = new byte[0x10];
            var expectedCmac = new byte[0x10];
            var counter = new byte[0x10];

            for (int i = 0; i < 0x20; i++)
            {
                if (keyblob_keys[i].IsEmpty() || keyblob_mac_keys[i].IsEmpty() || encrypted_keyblobs[i].IsEmpty())
                {
                    continue;
                }

                Array.Copy(encrypted_keyblobs[i], expectedCmac, 0x10);
                Crypto.CalculateAesCmac(keyblob_mac_keys[i], encrypted_keyblobs[i], 0x10, cmac, 0, 0xa0);

                if (!Util.ArraysEqual(cmac, expectedCmac))
                {
                    logger?.LogMessage($"Warning: Keyblob MAC {i:x2} is invalid. Are SBK/TSEC key correct?");
                }

                Array.Copy(encrypted_keyblobs[i], 0x10, counter, 0, 0x10);

                using (var keyblobDec = new RandomAccessSectorStream(new Aes128CtrStream(
                    new MemoryStream(encrypted_keyblobs[i], 0x20, keyblobs[i].Length), keyblob_keys[i], counter)))
                {
                    keyblobDec.Read(keyblobs[i], 0, keyblobs[i].Length);
                }
            }
        }

        private void ReadKeyblobs()
        {
            var masterKek = new byte[0x10];

            bool haveMasterKeySource = !master_key_source.IsEmpty();

            for (int i = 0; i < 0x20; i++)
            {
                if (keyblobs[i].IsEmpty()) continue;

                Array.Copy(keyblobs[i], 0x80, package1_keys[i], 0, 0x10);

                if (!haveMasterKeySource) continue;

                Array.Copy(keyblobs[i], masterKek, 0x10);

                Crypto.DecryptEcb(masterKek, master_key_source, master_keys[i], 0x10);
            }
        }

        private void DerivePerConsoleKeys()
        {
            // Derive the device key
            if (!per_console_key_source.IsEmpty() && !keyblob_keys[0].IsEmpty())
            {
                Crypto.DecryptEcb(keyblob_keys[0], per_console_key_source, device_key, 0x10);
            }

            // Derive BIS keys
            if (device_key.IsEmpty()
                || bis_key_source[0].IsEmpty()
                || bis_key_source[1].IsEmpty()
                || bis_key_source[2].IsEmpty()
                || bis_kek_source.IsEmpty()
                || aes_kek_generation_source.IsEmpty()
                || aes_key_generation_source.IsEmpty()
                || retail_specific_aes_key_source.IsEmpty())
            {
                return;
            }

            var kek = new byte[0x10];

            Crypto.DecryptEcb(device_key, retail_specific_aes_key_source, kek, 0x10);
            Crypto.DecryptEcb(kek, bis_key_source[0], bis_keys[0], 0x20);

            Crypto.GenerateKek(kek, bis_kek_source, device_key, aes_kek_generation_source, aes_key_generation_source);

            Crypto.DecryptEcb(kek, bis_key_source[1], bis_keys[1], 0x20);
            Crypto.DecryptEcb(kek, bis_key_source[2], bis_keys[2], 0x20);

            // BIS keys 2 and 3 are the same
            Array.Copy(bis_keys[2], bis_keys[3], 0x20);
        }

        private void DerivePerFirmwareKeys()
        {
            bool haveKakSource0 = !key_area_key_application_source.IsEmpty();
            bool haveKakSource1 = !key_area_key_ocean_source.IsEmpty();
            bool haveKakSource2 = !key_area_key_system_source.IsEmpty();
            bool haveTitleKekSource = !titlekek_source.IsEmpty();
            bool havePackage2KeySource = !package2_key_source.IsEmpty();

            for (int i = 0; i < 0x20; i++)
            {
                if (master_keys[i].IsEmpty())
                {
                    continue;
                }

                if (haveKakSource0)
                {
                    Crypto.GenerateKek(key_area_keys[i][0], key_area_key_application_source, master_keys[i],
                        aes_kek_generation_source, aes_key_generation_source);
                }

                if (haveKakSource1)
                {
                    Crypto.GenerateKek(key_area_keys[i][1], key_area_key_ocean_source, master_keys[i],
                        aes_kek_generation_source, aes_key_generation_source);
                }

                if (haveKakSource2)
                {
                    Crypto.GenerateKek(key_area_keys[i][2], key_area_key_system_source, master_keys[i],
                        aes_kek_generation_source, aes_key_generation_source);
                }

                if (haveTitleKekSource)
                {
                    Crypto.DecryptEcb(master_keys[i], titlekek_source, titlekeks[i], 0x10);
                }

                if (havePackage2KeySource)
                {
                    Crypto.DecryptEcb(master_keys[i], package2_key_source, package2_keys[i], 0x10);
                }
            }
        }

        private void DeriveNcaHeaderKey()
        {
            if (header_kek_source.IsEmpty() || header_key_source.IsEmpty() || master_keys[0].IsEmpty()) return;

            var headerKek = new byte[0x10];

            Crypto.GenerateKek(headerKek, header_kek_source, master_keys[0], aes_kek_generation_source,
                aes_key_generation_source);
            Crypto.DecryptEcb(headerKek, header_key_source, header_key, 0x20);
        }

        private void DeriveSdCardKeys()
        {
            var sdKek = new byte[0x10];
            Crypto.GenerateKek(sdKek, sd_card_kek_source, master_keys[0], aes_kek_generation_source, aes_key_generation_source);

            for (int k = 0; k < sd_card_key_sources.Length; k++)
            {
                for (int i = 0; i < 0x20; i++)
                {
                    sd_card_key_sources_specific[k][i] = (byte)(sd_card_key_sources[k][i] ^ sd_seed[i & 0xF]);
                }
            }

            for (int k = 0; k < sd_card_key_sources_specific.Length; k++)
            {
                Crypto.DecryptEcb(sdKek, sd_card_key_sources_specific[k], sd_card_keys[k], 0x20);
            }
        }
    }

    public static class ExternalKeys
    {
        private const int TitleKeySize = 0x10;
        private static readonly Dictionary<string, KeyValue> CommonKeyDict;
        private static readonly Dictionary<string, KeyValue> UniqueKeyDict;
        private static readonly Dictionary<string, KeyValue> AllKeyDict;

        static ExternalKeys()
        {
            var commonKeys = CreateCommonKeyList();
            var uniqueKeys = CreateUniqueKeyList();

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
                    var a = line.Split(',', '=');
                    if (a.Length != 2) continue;

                    var key = a[0].Trim();
                    var valueStr = a[1].Trim();

                    if (!keyDict.TryGetValue(key, out var kv))
                    {
                        logger?.LogMessage($"Failed to match key {key}");
                        continue;
                    }

                    var value = valueStr.ToBytes();
                    if (value.Length != kv.Size)
                    {
                        logger?.LogMessage($"Key {key} had incorrect size {value.Length}. (Expected {kv.Size})");
                        continue;
                    }

                    var dest = kv.GetKey(keyset);
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
                    var a = line.Split(',', '=');
                    if (a.Length != 2) continue;

                    var rightsId = a[0].Trim().ToBytes();
                    var titleKey = a[1].Trim().ToBytes();

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

        public static string PrintKeys(Keyset keyset)
        {
            var sb = new StringBuilder();
            int maxNameLength = CommonKeyDict.Values.Max(x => x.Name.Length);

            foreach (KeyValue keySlot in CommonKeyDict.Values.OrderBy(x => x.Name))
            {
                byte[] key = keySlot.GetKey(keyset);
                if (key.IsEmpty()) continue;

                var line = $"{keySlot.Name.PadRight(maxNameLength)} = {key.ToHexString()}";
                sb.AppendLine(line);
            }

            return sb.ToString();
        }

        private static List<KeyValue> CreateCommonKeyList()
        {
            var keys = new List<KeyValue>
            {
                new KeyValue("aes_kek_generation_source", 0x10, set => set.aes_kek_generation_source),
                new KeyValue("aes_key_generation_source", 0x10, set => set.aes_key_generation_source),
                new KeyValue("key_area_key_application_source", 0x10, set => set.key_area_key_application_source),
                new KeyValue("key_area_key_ocean_source", 0x10, set => set.key_area_key_ocean_source),
                new KeyValue("key_area_key_system_source", 0x10, set => set.key_area_key_system_source),
                new KeyValue("titlekek_source", 0x10, set => set.titlekek_source),
                new KeyValue("header_kek_source", 0x10, set => set.header_kek_source),
                new KeyValue("header_key_source", 0x20, set => set.header_key_source),
                new KeyValue("header_key", 0x20, set => set.header_key),
                new KeyValue("xci_header_key", 0x10, set => set.xci_header_key),
                //new KeyValue("encrypted_header_key", 0x20, set => set.header_key_source),
                new KeyValue("package2_key_source", 0x10, set => set.package2_key_source),
                new KeyValue("sd_card_kek_source", 0x10, set => set.sd_card_kek_source),
                new KeyValue("sd_card_nca_key_source", 0x20, set => set.sd_card_key_sources[1]),
                new KeyValue("sd_card_save_key_source", 0x20, set => set.sd_card_key_sources[0]),
                new KeyValue("master_key_source", 0x10, set => set.master_key_source),
                new KeyValue("keyblob_mac_key_source", 0x10, set => set.keyblob_mac_key_source),
                new KeyValue("eticket_rsa_kek", 0x10, set => set.eticket_rsa_kek),
                new KeyValue("retail_specific_aes_key_source", 0x10, set => set.retail_specific_aes_key_source),
                new KeyValue("per_console_key_source", 0x10, set => set.per_console_key_source),
                new KeyValue("bis_kek_source", 0x10, set => set.bis_kek_source)
            };

            for (int slot = 0; slot < 0x20; slot++)
            {
                int i = slot;
                keys.Add(new KeyValue($"keyblob_key_source_{i:x2}", 0x10, set => set.keyblob_key_sources[i]));
                keys.Add(new KeyValue($"keyblob_{i:x2}", 0x90, set => set.keyblobs[i]));
                keys.Add(new KeyValue($"master_key_{i:x2}", 0x10, set => set.master_keys[i]));
                keys.Add(new KeyValue($"package1_key_{i:x2}", 0x10, set => set.package1_keys[i]));
                keys.Add(new KeyValue($"package2_key_{i:x2}", 0x10, set => set.package2_keys[i]));
                keys.Add(new KeyValue($"titlekek_{i:x2}", 0x10, set => set.titlekeks[i]));
                keys.Add(new KeyValue($"key_area_key_application_{i:x2}", 0x10, set => set.key_area_keys[i][0]));
                keys.Add(new KeyValue($"key_area_key_ocean_{i:x2}", 0x10, set => set.key_area_keys[i][1]));
                keys.Add(new KeyValue($"key_area_key_system_{i:x2}", 0x10, set => set.key_area_keys[i][2]));
            }

            for (int slot = 0; slot < 3; slot++)
            {
                int i = slot;
                keys.Add(new KeyValue($"bis_key_source_{i:x2}", 0x20, set => set.bis_key_source[i]));
            }

            return keys;
        }

        private static List<KeyValue> CreateUniqueKeyList()
        {
            var keys = new List<KeyValue>
            {
                new KeyValue("secure_boot_key", 0x10, set => set.secure_boot_key),
                new KeyValue("tsec_key", 0x10, set => set.tsec_key),
                new KeyValue("device_key", 0x10, set => set.device_key),
                new KeyValue("sd_seed", 0x10, set => set.sd_seed)
            };

            for (int slot = 0; slot < 0x20; slot++)
            {
                int i = slot;
                keys.Add(new KeyValue($"keyblob_key_{i:x2}", 0x10, set => set.keyblob_keys[i]));
                keys.Add(new KeyValue($"keyblob_mac_key_{i:x2}", 0x10, set => set.keyblob_mac_keys[i]));
                keys.Add(new KeyValue($"encrypted_keyblob_{i:x2}", 0xB0, set => set.encrypted_keyblobs[i]));
            }

            for (int slot = 0; slot < 4; slot++)
            {
                int i = slot;
                keys.Add(new KeyValue($"bis_key_{i:x2}", 0x20, set => set.bis_keys[i]));
            }

            return keys;
        }

        private class KeyValue
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
}
