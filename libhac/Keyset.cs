// ReSharper disable InconsistentNaming

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace libhac
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
        public byte[] encrypted_header_key { get; set; } = new byte[0x20];
        public byte[] header_key { get; set; } = new byte[0x20];
        public byte[][] titlekeks { get; set; } = Util.CreateJaggedArray<byte[][]>(0x20, 0x10);
        public byte[][][] key_area_keys { get; set; } = Util.CreateJaggedArray<byte[][][]>(0x20, 3, 0x10);
        public byte[][] sd_card_keys { get; set; } = Util.CreateJaggedArray<byte[][]>(2, 0x20);
        public byte[] nca_hdr_fixed_key_modulus { get; set; } = new byte[0x100];
        public byte[] acid_fixed_key_modulus { get; set; } = new byte[0x100];
        public byte[] package2_fixed_key_modulus { get; set; } = new byte[0x100];
        public byte[] eticket_rsa_kek { get; set; } = new byte[0x10];

        public byte[] secure_boot_key { get; set; } = new byte[0x10];
        public byte[] tsec_key { get; set; } = new byte[0x10];
        public byte[] device_key { get; set; } = new byte[0x10];
        public byte[][] bis_keys { get; set; } = Util.CreateJaggedArray<byte[][]>(4, 0x20);
        public byte[] sd_seed { get; set; } = new byte[0x10];
        public RsaKey eticket_ext_key_rsa { get; set; }

        public Dictionary<byte[], byte[]> TitleKeys { get; } = new Dictionary<byte[], byte[]>(new ByteArray128BitComparer());

        public void SetSdSeed(byte[] sdseed)
        {
            Array.Copy(sdseed, sd_seed, sd_seed.Length);
            DeriveKeys();
        }

        internal void DeriveKeys()
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
        private static readonly Dictionary<string, KeyValue> KeyDict = CreateKeyDict();

        public static Keyset ReadKeyFile(string filename, string titleKeysFilename = null, string consoleKeysFilename = null, IProgressReport progress = null)
        {
            var keyset = new Keyset();

            if (filename != null) ReadMainKeys(keyset, filename, progress);
            if (consoleKeysFilename != null) ReadMainKeys(keyset, consoleKeysFilename, progress);
            if (titleKeysFilename != null) ReadTitleKeys(keyset, titleKeysFilename, progress);
            keyset.DeriveKeys();

            return keyset;
        }

        private static void ReadMainKeys(Keyset keyset, string filename, IProgressReport progress = null)
        {
            if (filename == null) return;

            using (var reader = new StreamReader(new FileStream(filename, FileMode.Open, FileAccess.Read)))
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
                    var a = line.Split(',');
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

        private static Dictionary<string, KeyValue> CreateKeyDict()
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
                new KeyValue("header_key_source", 0x20, set => set.encrypted_header_key),
                new KeyValue("header_key", 0x20, set => set.header_key),
                new KeyValue("encrypted_header_key", 0x20, set => set.encrypted_header_key),
                new KeyValue("package2_key_source", 0x10, set => set.package2_key_source),
                new KeyValue("sd_card_kek_source", 0x10, set => set.sd_card_kek_source),
                new KeyValue("sd_card_nca_key_source", 0x20, set => set.sd_card_key_sources[1]),
                new KeyValue("sd_card_save_key_source", 0x20, set => set.sd_card_key_sources[0]),
                new KeyValue("master_key_source", 0x10, set => set.master_key_source),
                new KeyValue("keyblob_mac_key_source", 0x10, set => set.keyblob_mac_key_source),
                new KeyValue("secure_boot_key", 0x10, set => set.secure_boot_key),
                new KeyValue("tsec_key", 0x10, set => set.tsec_key),
                new KeyValue("device_key", 0x10, set => set.device_key),
                new KeyValue("sd_seed", 0x10, set => set.sd_seed),
                new KeyValue("eticket_rsa_kek", 0x10, set => set.eticket_rsa_kek )
            };

            for (int slot = 0; slot < 0x20; slot++)
            {
                int i = slot;
                keys.Add(new KeyValue($"keyblob_key_source_{i:x2}", 0x10, set => set.keyblob_key_sources[i]));
                keys.Add(new KeyValue($"keyblob_key_{i:x2}", 0x10, set => set.keyblob_keys[i]));
                keys.Add(new KeyValue($"keyblob_mac_key_{i:x2}", 0x10, set => set.keyblob_mac_keys[i]));
                keys.Add(new KeyValue($"encrypted_keyblob_{i:x2}", 0xB0, set => set.encrypted_keyblobs[i]));
                keys.Add(new KeyValue($"keyblob_{i:x2}", 0x90, set => set.keyblobs[i]));
                keys.Add(new KeyValue($"master_key_{i:x2}", 0x10, set => set.master_keys[i]));
                keys.Add(new KeyValue($"package1_key_{i:x2}", 0x10, set => set.package1_keys[i]));
                keys.Add(new KeyValue($"package2_key_{i:x2}", 0x10, set => set.package2_keys[i]));
                keys.Add(new KeyValue($"titlekek_{i:x2}", 0x10, set => set.titlekeks[i]));
                keys.Add(new KeyValue($"key_area_key_application_{i:x2}", 0x10, set => set.key_area_keys[i][0]));
                keys.Add(new KeyValue($"key_area_key_ocean_{i:x2}", 0x10, set => set.key_area_keys[i][1]));
                keys.Add(new KeyValue($"key_area_key_system_{i:x2}", 0x10, set => set.key_area_keys[i][2]));
            }

            for (int slot = 0; slot < 4; slot++)
            {
                int i = slot;
                keys.Add(new KeyValue($"bis_key_{i:x2}", 0x20, set => set.bis_keys[i]));
            }

            return keys.ToDictionary(k => k.Name, k => k);
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
