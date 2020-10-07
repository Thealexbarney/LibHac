using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Spl;

namespace LibHac.Common.Keys
{
    public static class ExternalKeyReader
    {
        [DebuggerDisplay("{" + nameof(Name) + "}")]
        public readonly struct KeyInfo
        {
            public readonly string Name;
            public readonly KeyGetter Getter;
            public readonly int Group;
            public readonly KeyRangeType RangeType;
            public readonly KeyType Type;
            public readonly byte RangeStart;
            public readonly byte RangeEnd;

            public int NameLength => Name.Length + (RangeType == KeyRangeType.Range ? 3 : 0);

            public delegate Span<byte> KeyGetter(KeySet keySet, int i);

            public KeyInfo(int group, KeyType type, string name, KeyGetter retrieveFunc)
            {
                Assert.AssertTrue(IsKeyTypeValid(type));

                Name = name;
                RangeType = KeyRangeType.Single;
                Type = type;
                RangeStart = default;
                RangeEnd = default;
                Group = group;
                Getter = retrieveFunc;
            }

            public KeyInfo(int group, KeyType type, string name, byte rangeStart, byte rangeEnd, KeyGetter retrieveFunc)
            {
                Assert.AssertTrue(IsKeyTypeValid(type));

                Name = name;
                RangeType = KeyRangeType.Range;
                Type = type;
                RangeStart = rangeStart;
                RangeEnd = rangeEnd;
                Group = group;
                Getter = retrieveFunc;
            }

            public bool Matches(string keyName, out int keyIndex, out bool isDev)
            {
                keyIndex = default;
                isDev = default;

                return RangeType switch
                {
                    KeyRangeType.Single => MatchesSingle(keyName, out isDev),
                    KeyRangeType.Range => MatchesRangedKey(keyName, ref keyIndex, out isDev),
                    _ => false
                };
            }

            private bool MatchesSingle(string keyName, out bool isDev)
            {
                Assert.Equal((int)KeyRangeType.Single, (int)RangeType);

                isDev = false;

                if (keyName.Length == NameLength + 4)
                {
                    // Might be a dev key. Check if "_dev" comes after the base key name
                    if (!keyName.AsSpan(Name.Length, 4).SequenceEqual("_dev"))
                        return false;

                    isDev = true;
                }
                else if (keyName.Length != NameLength)
                {
                    return false;
                }

                // Check if the base name matches
                if (!keyName.AsSpan(0, Name.Length).SequenceEqual(Name))
                    return false;

                return true;
            }

            private bool MatchesRangedKey(string keyName, ref int keyIndex, out bool isDev)
            {
                Assert.Equal((int)KeyRangeType.Range, (int)RangeType);

                isDev = false;

                // Check if this is an explicit dev key
                if (keyName.Length == Name.Length + 7)
                {
                    // Check if "_dev" comes after the base key name
                    if (!keyName.AsSpan(Name.Length, 4).SequenceEqual("_dev"))
                        return false;

                    isDev = true;
                }
                // Not a dev key. Check that the length of the key name with the trailing index matches
                else if (keyName.Length != Name.Length + 3)
                    return false;

                // Check if the name before the "_XX" index matches
                if (!keyName.AsSpan(0, Name.Length).SequenceEqual(Name))
                    return false;

                // The name should have an underscore before the index value
                if (keyName[keyName.Length - 3] != '_')
                    return false;

                byte index = default;

                // Try to get the index of the key name
                if (!keyName.AsSpan(keyName.Length - 2, 2).TryToBytes(SpanHelpers.AsSpan(ref index)))
                    return false;

                // Check if the index is in this key's range
                if (index < RangeStart || index >= RangeEnd)
                    return false;

                keyIndex = index;
                return true;
            }

            private static bool IsKeyTypeValid(KeyType type)
            {
                // Make sure the type has exactly one flag set for each type
                KeyType type1 = type & (KeyType.Common | KeyType.Device);
                KeyType type2 = type & (KeyType.Root | KeyType.Seed | KeyType.Derived);

                bool isValid1 = type1 == KeyType.Common || type1 == KeyType.Device;
                bool isValid2 = type2 == KeyType.Root || type2 == KeyType.Seed || type2 == KeyType.Derived;

                return isValid1 && isValid2;
            }
        }

        // Contains info from a specific key being read from a file
        [DebuggerDisplay("{" + nameof(Name) + "}")]
        private struct SpecificKeyInfo
        {
            public KeyInfo Key;
            public int Index;
            public bool IsDev;

            public string Name => Key.Name;

            public SpecificKeyInfo(KeyInfo info, int index, bool isDev)
            {
                Key = info;
                Index = index;
                IsDev = isDev;
            }
        }

        public enum KeyRangeType : byte
        {
            Single,
            Range
        }

        [Flags]
        public enum KeyType : byte
        {
            Common = 1 << 0,
            Device = 1 << 1,
            Root = 1 << 2,
            Seed = 1 << 3,
            Derived = 1 << 4,

            /// <summary>Specifies that a seed is different in prod and dev.</summary>
            DifferentDev = 1 << 5,

            CommonRoot = Common | Root,
            CommonSeed = Common | Seed,
            CommonSeedDiff = Common | Seed | DifferentDev,
            CommonDrvd = Common | Derived,
            DeviceRoot = Device | Root,
            DeviceDrvd = Device | Derived,
        }

        private const int TitleKeySize = 0x10;

        /// <summary>
        /// Loads keys from key files into an existing <see cref="KeySet"/>. Missing keys will be
        /// derived from existing keys if possible. Any <see langword="null"/> file names will be skipped.
        /// </summary>
        /// <param name="keySet">The <see cref="KeySet"/> where the loaded keys will be placed.</param>
        /// <param name="filename">The path of the file containing common keys. Can be <see langword="null"/>.</param>
        /// <param name="titleKeysFilename">The path of the file containing title keys. Can be <see langword="null"/>.</param>
        /// <param name="consoleKeysFilename">The path of the file containing device-unique keys. Can be <see langword="null"/>.</param>
        /// <param name="logger">An optional logger that key-parsing errors will be written to.</param>
        public static void ReadKeyFile(KeySet keySet, string filename, string titleKeysFilename = null,
            string consoleKeysFilename = null, IProgressReport logger = null)
        {
            List<KeyInfo> keyInfos = CreateKeyList();

            if (filename != null)
            {
                using var reader = new StreamReader(new FileStream(filename, FileMode.Open, FileAccess.Read));
                ReadMainKeys(keySet, reader, keyInfos, logger);
            }

            if (consoleKeysFilename != null)
            {
                using var reader = new StreamReader(new FileStream(consoleKeysFilename, FileMode.Open, FileAccess.Read));
                ReadMainKeys(keySet, reader, keyInfos, logger);
            }

            if (titleKeysFilename != null)
            {
                using var reader = new StreamReader(new FileStream(titleKeysFilename, FileMode.Open, FileAccess.Read));
                ReadTitleKeys(keySet, reader, logger);
            }

            keySet.DeriveKeys(logger);

            // Dev keys can read from prod key files, so derive any missing keys if necessary.
            if (keySet.CurrentMode == KeySet.Mode.Prod)
            {
                keySet.SetMode(KeySet.Mode.Dev);
                keySet.DeriveKeys(logger);
                keySet.SetMode(KeySet.Mode.Prod);
            }
        }

        /// <summary>
        /// Creates a new <see cref="KeySet"/> initialized with the key files specified and any keys included in the library.
        /// Missing keys will be derived from existing keys if possible. Any <see langword="null"/> file names will be skipped.
        /// </summary>
        /// <param name="filename">The path of the file containing common keys. Can be <see langword="null"/>.</param>
        /// <param name="titleKeysFilename">The path of the file containing title keys. Can be <see langword="null"/>.</param>
        /// <param name="consoleKeysFilename">The path of the file containing device-unique keys. Can be <see langword="null"/>.</param>
        /// <param name="logger">An optional logger that key-parsing errors will be written to.</param>
        /// <param name="mode">Specifies whether the keys being read are dev or prod keys.</param>
        /// <returns>The created <see cref="KeySet"/>.</returns>
        public static KeySet ReadKeyFile(string filename, string titleKeysFilename = null,
            string consoleKeysFilename = null, IProgressReport logger = null, KeySet.Mode mode = KeySet.Mode.Prod)
        {
            var keySet = KeySet.CreateDefaultKeySet();
            keySet.SetMode(mode);

            ReadKeyFile(keySet, filename, titleKeysFilename, consoleKeysFilename, logger);

            return keySet;
        }

        /// <summary>
        /// Loads non-title keys from a <see cref="TextReader"/> into an existing <see cref="KeySet"/>.
        /// Missing keys will not be derived.
        /// </summary>
        /// <param name="keySet">The <see cref="KeySet"/> where the loaded keys will be placed.</param>
        /// <param name="keyFileReader">A <see cref="TextReader"/> containing the keys to load.</param>
        /// <param name="keyList">A list of all the keys that will be loaded into the key set.
        /// <see cref="CreateKeyList"/> will create a list containing all loadable keys.</param>
        /// <param name="logger">An optional logger that key-parsing errors will be written to.</param>
        public static void ReadMainKeys(KeySet keySet, TextReader keyFileReader, List<KeyInfo> keyList,
            IProgressReport logger = null)
        {
            if (keyFileReader == null) return;

            // Todo: Improve key parsing
            string line;
            while ((line = keyFileReader.ReadLine()) != null)
            {
                string[] a = line.Split(',', '=');
                if (a.Length != 2) continue;

                string keyName = a[0].Trim();
                string valueStr = a[1].Trim();

                if (!TryGetKeyInfo(out SpecificKeyInfo info, keyList, keyName))
                {
                    logger?.LogMessage($"Failed to match key {keyName}");
                    continue;
                }

                Span<byte> key;

                // Get the dev key in the key set if needed.
                if (info.IsDev && keySet.CurrentMode == KeySet.Mode.Prod)
                {
                    keySet.SetMode(KeySet.Mode.Dev);
                    key = info.Key.Getter(keySet, info.Index);
                    keySet.SetMode(KeySet.Mode.Prod);
                }
                else
                {
                    key = info.Key.Getter(keySet, info.Index);
                }

                if (valueStr.Length != key.Length * 2)
                {
                    logger?.LogMessage(
                        $"Key {keyName} had incorrect size {valueStr.Length}. Must be {key.Length * 2} hex digits.");
                    continue;
                }

                if (!Utilities.TryToBytes(valueStr, key))
                {
                    key.Clear();

                    logger?.LogMessage($"Key {keyName} had an invalid value. Must be {key.Length * 2} hex digits.");
                }
            }
        }

        /// <summary>
        /// Loads title keys from a <see cref="TextReader"/> into an existing <see cref="KeySet"/>.
        /// </summary>
        /// <param name="keySet">The <see cref="KeySet"/> where the loaded keys will be placed.</param>
        /// <param name="keyFileReader">A <see cref="TextReader"/> containing the keys to load.</param>
        /// <param name="logger">An optional logger that key-parsing errors will be written to.</param>
        public static void ReadTitleKeys(KeySet keySet, TextReader keyFileReader, IProgressReport logger = null)
        {
            if (keyFileReader == null) return;

            // Todo: Improve key parsing
            string line;
            while ((line = keyFileReader.ReadLine()) != null)
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
                    logger?.LogMessage($"Invalid rights ID \"{splitLine[0].Trim()}\" in title key file");
                    continue;
                }

                if (!splitLine[1].Trim().TryToBytes(out byte[] titleKey))
                {
                    logger?.LogMessage($"Invalid title key \"{splitLine[1].Trim()}\" in title key file");
                    continue;
                }

                if (rightsId.Length != TitleKeySize)
                {
                    logger?.LogMessage($"Rights ID {rightsId.ToHexString()} had incorrect size {rightsId.Length}. (Expected {TitleKeySize})");
                    continue;
                }

                if (titleKey.Length != TitleKeySize)
                {
                    logger?.LogMessage($"Title key {titleKey.ToHexString()} had incorrect size {titleKey.Length}. (Expected {TitleKeySize})");
                    continue;
                }

                keySet.ExternalKeySet.Add(new RightsId(rightsId), new AccessKey(titleKey)).ThrowIfFailure();
            }
        }

        private static bool TryGetKeyInfo(out SpecificKeyInfo info, List<KeyInfo> keyList, string keyName)
        {
            for (int i = 0; i < keyList.Count; i++)
            {
                if (keyList[i].Matches(keyName, out int keyIndex, out bool isDev))
                {
                    info = new SpecificKeyInfo(keyList[i], keyIndex, isDev);
                    return true;
                }
            }

            info = default;
            return false;
        }

        public static void PrintKeys(KeySet keySet, StringBuilder sb, List<KeyInfo> keys, KeyType filter, bool isDev)
        {
            if (keys.Count == 0) return;

            string devSuffix = isDev ? "_dev" : string.Empty;
            int maxNameLength = keys.Max(x => x.NameLength);
            int currentGroup = 0;

            // Todo: Better filtering
            bool FilterMatches(KeyInfo keyInfo)
            {
                KeyType filter1 = filter & (KeyType.Common | KeyType.Device);
                KeyType filter2 = filter & (KeyType.Root | KeyType.Seed | KeyType.Derived);
                KeyType filter3 = filter & KeyType.DifferentDev;

                // Skip sub-filters that have no flags set
                return (filter1 == 0 || (filter1 & keyInfo.Type) != 0) &&
                       (filter2 == 0 || (filter2 & keyInfo.Type) != 0) &&
                       filter3 == (filter3 & keyInfo.Type) ||
                       isDev && keyInfo.Type.HasFlag(KeyType.DifferentDev);
            }

            bool isFirstPrint = true;

            foreach (KeyInfo info in keys.Where(x => x.Group >= 0).Where(FilterMatches)
                .OrderBy(x => x.Group).ThenBy(x => x.Name))
            {
                bool isNewGroup = false;

                if (info.Group == currentGroup + 1)
                {
                    currentGroup = info.Group;
                }
                else if (info.Group > currentGroup)
                {
                    // Don't update the current group yet because if this key is empty and the next key
                    // is in the same group, we need to be able to know to add a blank line before printing it.
                    isNewGroup = !isFirstPrint;
                }

                if (info.RangeType == KeyRangeType.Single)
                {
                    Span<byte> key = info.Getter(keySet, 0);
                    if (key.IsEmpty())
                        continue;

                    if (isNewGroup)
                    {
                        sb.AppendLine();
                    }

                    string keyName = $"{info.Name}{devSuffix}";

                    string line = $"{keyName.PadRight(maxNameLength)} = {key.ToHexString()}";
                    sb.AppendLine(line);
                    isFirstPrint = false;
                    currentGroup = info.Group;
                }
                else if (info.RangeType == KeyRangeType.Range)
                {
                    bool hasPrintedKey = false;

                    for (int i = info.RangeStart; i < info.RangeEnd; i++)
                    {
                        Span<byte> key = info.Getter(keySet, i);
                        if (key.IsEmpty())
                            continue;

                        if (hasPrintedKey == false)
                        {
                            if (isNewGroup)
                            {
                                sb.AppendLine();
                            }

                            hasPrintedKey = true;
                        }

                        string keyName = $"{info.Name}{devSuffix}_{i:x2}";

                        string line = $"{keyName.PadRight(maxNameLength)} = {key.ToHexString()}";
                        sb.AppendLine(line);
                        isFirstPrint = false;
                        currentGroup = info.Group;
                    }
                }
            }
        }

        public static string PrintTitleKeys(KeySet keySet)
        {
            var sb = new StringBuilder();

            foreach ((RightsId rightsId, AccessKey key) kv in keySet.ExternalKeySet.ToList()
                .OrderBy(x => x.rightsId.ToString()))
            {
                string line = $"{kv.rightsId} = {kv.key}";
                sb.AppendLine(line);
            }

            return sb.ToString();
        }

        public static string PrintCommonKeys(KeySet keySet)
        {
            var sb = new StringBuilder();
            PrintKeys(keySet, sb, CreateKeyList(), KeyType.Common | KeyType.Root | KeyType.Seed | KeyType.Derived, false);
            return sb.ToString();
        }

        public static string PrintDeviceKeys(KeySet keySet)
        {
            var sb = new StringBuilder();
            PrintKeys(keySet, sb, CreateKeyList(), KeyType.Device, false);
            return sb.ToString();
        }

        public static string PrintAllKeys(KeySet keySet)
        {
            var sb = new StringBuilder();
            PrintKeys(keySet, sb, CreateKeyList(), 0, false);
            return sb.ToString();
        }

        public static string PrintAllSeeds(KeySet keySet)
        {
            var sb = new StringBuilder();
            PrintKeys(keySet, sb, CreateKeyList(), KeyType.Common | KeyType.Seed, false);

            if (keySet.CurrentMode == KeySet.Mode.Prod)
            {
                sb.AppendLine();
                keySet.SetMode(KeySet.Mode.Dev);
                PrintKeys(keySet, sb, CreateKeyList(), KeyType.Common | KeyType.Seed | KeyType.DifferentDev, true);
                keySet.SetMode(KeySet.Mode.Prod);
            }
            return sb.ToString();
        }

        public static string PrintCommonKeysWithDev(KeySet keySet)
        {
            var sb = new StringBuilder();
            PrintKeys(keySet, sb, CreateKeyList(), KeyType.Common | KeyType.Root | KeyType.Seed | KeyType.Derived, false);

            if (keySet.CurrentMode == KeySet.Mode.Prod)
            {
                sb.AppendLine();
                keySet.SetMode(KeySet.Mode.Dev);
                PrintKeys(keySet, sb, CreateKeyList(), KeyType.Common | KeyType.Root | KeyType.Derived, true);
                keySet.SetMode(KeySet.Mode.Prod);
            }

            return sb.ToString();
        }

        public static List<KeyInfo> CreateKeyList()
        {
            // Update this value if more keys are added
            var keys = new List<KeyInfo>(70);

            // Keys with a group value of -1 are keys that will be read but not written.
            // This is for compatibility since some keys had other names in the past.

            // TSEC secrets aren't public yet, so the TSEC root keys will be treated as
            // root keys even though they're derived.

            keys.Add(new KeyInfo(10, KeyType.DeviceRoot, "secure_boot_key", (set, i) => set.SecureBootKey));
            keys.Add(new KeyInfo(11, KeyType.DeviceRoot, "tsec_key", (set, i) => set.TsecKey));
            keys.Add(new KeyInfo(12, KeyType.DeviceDrvd, "device_key", (set, i) => set.DeviceKey));

            keys.Add(new KeyInfo(20, KeyType.CommonRoot, "tsec_root_kek", (set, i) => set.TsecRootKek));
            keys.Add(new KeyInfo(21, KeyType.CommonRoot, "package1_mac_kek", (set, i) => set.Package1MacKek));
            keys.Add(new KeyInfo(22, KeyType.CommonRoot, "package1_kek", (set, i) => set.Package1Kek));

            keys.Add(new KeyInfo(30, KeyType.CommonRoot, "tsec_auth_signature", 0, 0x20, (set, i) => set.TsecAuthSignatures[i]));

            keys.Add(new KeyInfo(40, KeyType.CommonRoot, "tsec_root_key", 0, 0x20, (set, i) => set.TsecRootKeys[i]));

            keys.Add(new KeyInfo(50, KeyType.CommonSeed, "keyblob_mac_key_source", (set, i) => set.KeyBlobMacKeySource));
            keys.Add(new KeyInfo(51, KeyType.CommonSeed, "keyblob_key_source", 0, 6, (set, i) => set.KeyBlobKeySources[i]));

            keys.Add(new KeyInfo(55, KeyType.DeviceDrvd, "keyblob_key", 0, 6, (set, i) => set.KeyBlobKeys[i]));

            keys.Add(new KeyInfo(60, KeyType.DeviceDrvd, "keyblob_mac_key", 0, 6, (set, i) => set.KeyBlobMacKeys[i]));

            keys.Add(new KeyInfo(70, KeyType.DeviceRoot, "encrypted_keyblob", 0, 6, (set, i) => set.EncryptedKeyBlobs[i].Bytes));

            keys.Add(new KeyInfo(80, KeyType.CommonRoot, "keyblob", 0, 6, (set, i) => set.KeyBlobs[i].Bytes));

            keys.Add(new KeyInfo(90, KeyType.CommonSeed, "master_kek_source", 6, 0x20, (set, i) => set.MasterKekSources[i]));

            keys.Add(new KeyInfo(100, KeyType.CommonRoot, "mariko_bek", (set, i) => set.MarikoBek));
            keys.Add(new KeyInfo(101, KeyType.CommonRoot, "mariko_kek", (set, i) => set.MarikoKek));

            keys.Add(new KeyInfo(110, KeyType.CommonRoot, "mariko_aes_class_key", 0, 0xC, (set, i) => set.MarikoAesClassKeys[i]));
            keys.Add(new KeyInfo(120, KeyType.CommonSeedDiff, "mariko_master_kek_source", 0, 0x20, (set, i) => set.MarikoMasterKekSources[i]));
            keys.Add(new KeyInfo(130, KeyType.CommonDrvd, "master_kek", 0, 0x20, (set, i) => set.MasterKeks[i]));
            keys.Add(new KeyInfo(140, KeyType.CommonSeed, "master_key_source", (set, i) => set.MasterKeySource));
            keys.Add(new KeyInfo(150, KeyType.CommonDrvd, "master_key", 0, 0x20, (set, i) => set.MasterKeys[i]));

            keys.Add(new KeyInfo(160, KeyType.CommonDrvd, "package1_key", 0, 0x20, (set, i) => set.Package1Keys[i]));
            keys.Add(new KeyInfo(170, KeyType.CommonDrvd, "package1_mac_key", 6, 0x20, (set, i) => set.Package1MacKeys[i]));
            keys.Add(new KeyInfo(180, KeyType.CommonSeed, "package2_key_source", (set, i) => set.Package2KeySource));
            keys.Add(new KeyInfo(190, KeyType.CommonDrvd, "package2_key", 0, 0x20, (set, i) => set.Package2Keys[i]));

            keys.Add(new KeyInfo(200, KeyType.CommonSeed, "bis_kek_source", (set, i) => set.BisKekSource));
            keys.Add(new KeyInfo(201, KeyType.CommonSeed, "bis_key_source", 0, 4, (set, i) => set.BisKeySources[i]));

            keys.Add(new KeyInfo(205, KeyType.DeviceDrvd, "bis_key", 0, 4, (set, i) => set.BisKeys[i]));

            keys.Add(new KeyInfo(210, KeyType.CommonSeed, "per_console_key_source", (set, i) => set.PerConsoleKeySource));
            keys.Add(new KeyInfo(211, KeyType.CommonSeed, "retail_specific_aes_key_source", (set, i) => set.RetailSpecificAesKeySource));
            keys.Add(new KeyInfo(212, KeyType.CommonSeed, "aes_kek_generation_source", (set, i) => set.AesKekGenerationSource));
            keys.Add(new KeyInfo(213, KeyType.CommonSeed, "aes_key_generation_source", (set, i) => set.AesKeyGenerationSource));
            keys.Add(new KeyInfo(214, KeyType.CommonSeed, "titlekek_source", (set, i) => set.TitleKekSource));

            keys.Add(new KeyInfo(220, KeyType.CommonDrvd, "titlekek", 0, 0x20, (set, i) => set.TitleKeks[i]));

            keys.Add(new KeyInfo(230, KeyType.CommonSeed, "header_kek_source", (set, i) => set.HeaderKekSource));
            keys.Add(new KeyInfo(231, KeyType.CommonSeed, "header_key_source", (set, i) => set.HeaderKeySource));
            keys.Add(new KeyInfo(232, KeyType.CommonDrvd, "header_key", (set, i) => set.HeaderKey));

            keys.Add(new KeyInfo(240, KeyType.CommonSeed, "key_area_key_application_source", (set, i) => set.KeyAreaKeyApplicationSource));
            keys.Add(new KeyInfo(241, KeyType.CommonSeed, "key_area_key_ocean_source", (set, i) => set.KeyAreaKeyOceanSource));
            keys.Add(new KeyInfo(242, KeyType.CommonSeed, "key_area_key_system_source", (set, i) => set.KeyAreaKeySystemSource));

            keys.Add(new KeyInfo(250, KeyType.CommonSeed, "save_mac_kek_source", (set, i) => set.DeviceUniqueSaveMacKekSource));
            keys.Add(new KeyInfo(251, KeyType.CommonSeed, "save_mac_key_source", 0, 2, (set, i) => set.DeviceUniqueSaveMacKeySources[i]));
            keys.Add(new KeyInfo(252, KeyType.DeviceDrvd, "save_mac_key", 0, 2, (set, i) => set.DeviceUniqueSaveMacKeys[i]));
            keys.Add(new KeyInfo(-01, KeyType.CommonSeed, "save_mac_key_source", (set, i) => set.DeviceUniqueSaveMacKeySources[0]));

            keys.Add(new KeyInfo(253, KeyType.CommonSeed, "save_mac_sd_card_kek_source", (set, i) => set.SeedUniqueSaveMacKekSource));
            keys.Add(new KeyInfo(254, KeyType.CommonSeed, "save_mac_sd_card_key_source", (set, i) => set.SeedUniqueSaveMacKeySource));
            keys.Add(new KeyInfo(255, KeyType.DeviceDrvd, "save_mac_sd_card_key", (set, i) => set.SeedUniqueSaveMacKey));

            keys.Add(new KeyInfo(260, KeyType.DeviceRoot, "sd_seed", (set, i) => set.SdCardEncryptionSeed));

            keys.Add(new KeyInfo(261, KeyType.CommonSeed, "sd_card_kek_source", (set, i) => set.SdCardKekSource));
            keys.Add(new KeyInfo(262, KeyType.CommonSeed, "sd_card_save_key_source", (set, i) => set.SdCardKeySources[0]));
            keys.Add(new KeyInfo(263, KeyType.CommonSeed, "sd_card_nca_key_source", (set, i) => set.SdCardKeySources[1]));
            keys.Add(new KeyInfo(264, KeyType.CommonSeed, "sd_card_custom_storage_key_source", (set, i) => set.SdCardKeySources[2]));

            keys.Add(new KeyInfo(270, KeyType.CommonSeedDiff, "xci_header_key", (set, i) => set.XciHeaderKey));

            keys.Add(new KeyInfo(280, KeyType.CommonRoot, "eticket_rsa_kek", (set, i) => set.ETicketRsaKek));
            keys.Add(new KeyInfo(281, KeyType.CommonRoot, "ssl_rsa_kek", (set, i) => set.SslRsaKek));

            keys.Add(new KeyInfo(290, KeyType.CommonDrvd, "key_area_key_application", 0, 0x20, (set, i) => set.KeyAreaKeys[i][0]));
            keys.Add(new KeyInfo(300, KeyType.CommonDrvd, "key_area_key_ocean", 0, 0x20, (set, i) => set.KeyAreaKeys[i][1]));
            keys.Add(new KeyInfo(310, KeyType.CommonDrvd, "key_area_key_system", 0, 0x20, (set, i) => set.KeyAreaKeys[i][2]));

            return keys;
        }
    }
}
