// ReSharper disable InconsistentNaming

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace libhac
{
    public class Keyset
    {
        public byte[] secure_boot_key { get; set; } = new byte[0x10];
        public byte[] tsec_key { get; set; } = new byte[0x10];
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
        public byte[] encrypted_header_key { get; set; } = new byte[0x20];
        public byte[] header_key { get; set; } = new byte[0x20];
        public byte[][] titlekeks { get; set; } = Util.CreateJaggedArray<byte[][]>(0x20, 0x10);
        public byte[][][] key_area_keys { get; set; } = Util.CreateJaggedArray<byte[][][]>(0x20, 3, 0x10);
        public byte[][] sd_card_keys { get; set; } = Util.CreateJaggedArray<byte[][]>(2, 0x20);
        public byte[] nca_hdr_fixed_key_modulus { get; set; } = new byte[0x100];
        public byte[] acid_fixed_key_modulus { get; set; } = new byte[0x100];
        public byte[] package2_fixed_key_modulus { get; set; } = new byte[0x100];

        public void SetSdSeed(byte[] sdseed)
        {
            for (int k = 0; k < sd_card_key_sources.Length; k++)
            {
                for (int i = 0; i < 0x20; i++)
                {
                    sd_card_key_sources_specific[k][i] = (byte)(sd_card_key_sources[k][i] ^ sdseed[i & 0xF]);
                }
            }

            DeriveKeys();
        }

        private void DeriveKeys()
        {
            //var cmac = new byte[0x10];
            //for (int i = 0; i < 0x20; i++)
            //{
            //    Crypto.DecryptEcb(tsec_key, keyblob_key_sources[i], keyblob_keys[i], 0x10);
            //    Crypto.DecryptEcb(secure_boot_key, keyblob_keys[i], keyblob_keys[i], 0x10);
            //    Crypto.DecryptEcb(keyblob_keys[i], keyblob_mac_key_source, keyblob_mac_keys[i], 0x10);
            //}

            var sdKek = new byte[0x10];
            Crypto.GenerateKek(sdKek, sd_card_kek_source, master_keys[0], aes_kek_generation_source, aes_key_generation_source);

            for (int k = 0; k < sd_card_key_sources_specific.Length; k++)
            {
                Crypto.DecryptEcb(sdKek, sd_card_key_sources_specific[k], sd_card_keys[k], 0x20);
            }
        }
    }

    public static class ExternalKeys
    {
        private static readonly Dictionary<string, KeyValue> KeyDict = CreateKeyDict();

        public static Keyset ReadKeyFile(string filename, IProgressReport progress = null)
        {
            var keyset = new Keyset();
            using (var reader = new StreamReader(new FileStream(filename, FileMode.Open)))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var a = line.Split('=');
                    if (a.Length != 2) continue;

                    var key = a[0].Trim();
                    var valueStr = a[1].Trim();

                    if (!KeyDict.TryGetValue(key, out var kv))
                    {
                        progress?.LogMessage($"Failed to match key {key}");
                        continue;
                    }

                    var value = valueStr.ToBytes();
                    if (value.Length != kv.Size)
                    {
                        progress?.LogMessage($"Key {key} had incorrect size {value.Length}. (Expected {kv.Size})");
                        continue;
                    }

                    kv.Assign(keyset, value);
                }
            }

            return keyset;
        }

        private static Dictionary<string, KeyValue> CreateKeyDict()
        {
            var keys = new List<KeyValue>
            {
                new KeyValue("aes_kek_generation_source", 0x10, (set, key) => set.aes_kek_generation_source = key),
                new KeyValue("aes_key_generation_source", 0x10, (set, key) => set.aes_key_generation_source = key),
                new KeyValue("key_area_key_application_source", 0x10, (set, key) => set.key_area_key_application_source = key),
                new KeyValue("key_area_key_ocean_source", 0x10, (set, key) => set.key_area_key_ocean_source = key),
                new KeyValue("key_area_key_system_source", 0x10, (set, key) => set.key_area_key_system_source = key),
                new KeyValue("titlekek_source", 0x10, (set, key) => set.titlekek_source = key),
                new KeyValue("header_kek_source", 0x10, (set, key) => set.header_kek_source = key),
                new KeyValue("header_key_source", 0x20, (set, key) => set.encrypted_header_key = key),
                new KeyValue("header_key", 0x20, (set, key) => set.header_key = key),
                new KeyValue("encrypted_header_key", 0x20, (set, key) => set.encrypted_header_key = key),
                new KeyValue("package2_key_source", 0x10, (set, key) => set.package2_key_source = key),
                new KeyValue("sd_card_kek_source", 0x10, (set, key) => set.sd_card_kek_source = key),
                new KeyValue("sd_card_nca_key_source", 0x20, (set, key) => set.sd_card_key_sources[1] = key),
                new KeyValue("sd_card_save_key_source", 0x20, (set, key) => set.sd_card_key_sources[0] = key),
                new KeyValue("master_key_source", 0x10, (set, key) => set.master_key_source = key),
                new KeyValue("keyblob_mac_key_source", 0x10, (set, key) => set.keyblob_mac_key_source = key),
                new KeyValue("secure_boot_key", 0x10, (set, key) => set.secure_boot_key = key),
                new KeyValue("tsec_key", 0x10, (set, key) => set.tsec_key = key)
            };

            for (int slot = 0; slot < 0x20; slot++)
            {
                int i = slot;
                keys.Add(new KeyValue($"keyblob_key_source_{i:D2}", 0x10, (set, key) => set.keyblob_key_sources[i] = key));
                keys.Add(new KeyValue($"keyblob_key_{i:D2}", 0x10, (set, key) => set.keyblob_keys[i] = key));
                keys.Add(new KeyValue($"keyblob_mac_key_{i:D2}", 0x10, (set, key) => set.keyblob_mac_keys[i] = key));
                keys.Add(new KeyValue($"encrypted_keyblob_{i:D2}", 0xB0, (set, key) => set.encrypted_keyblobs[i] = key));
                keys.Add(new KeyValue($"keyblob_{i:D2}", 0x90, (set, key) => set.keyblobs[i] = key));
                keys.Add(new KeyValue($"master_key_{i:D2}", 0x10, (set, key) => set.master_keys[i] = key));
                keys.Add(new KeyValue($"package1_key_{i:D2}", 0x10, (set, key) => set.package1_keys[i] = key));
                keys.Add(new KeyValue($"package2_key_{i:D2}", 0x10, (set, key) => set.package2_keys[i] = key));
                keys.Add(new KeyValue($"titlekek_{i:D2}", 0x10, (set, key) => set.titlekeks[i] = key));
                keys.Add(new KeyValue($"key_area_key_application_{i:D2}", 0x10, (set, key) => set.key_area_keys[i][0] = key));
                keys.Add(new KeyValue($"key_area_key_ocean_{i:D2}", 0x10, (set, key) => set.key_area_keys[i][1] = key));
                keys.Add(new KeyValue($"key_area_key_system_{i:D2}", 0x10, (set, key) => set.key_area_keys[i][2] = key));
            }

            return keys.ToDictionary(k => k.Name, k => k);
        }

        private class KeyValue
        {
            public string Name;
            public int Size;
            public Action<Keyset, byte[]> Assign;

            public KeyValue(string name, int size, Action<Keyset, byte[]> assign)
            {
                Name = name;
                Size = size;
                Assign = assign;
            }
        }
    }
}
