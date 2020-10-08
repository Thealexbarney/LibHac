using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using LibHac.Fs;
using LibHac.Spl;

namespace LibHac.Common.Keys
{
    public static class ExternalKeyReader
    {
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
            List<KeyInfo> keyInfos = DefaultKeySet.CreateKeyList();

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
        /// <see cref="DefaultKeySet.CreateKeyList"/> will create a list containing all loadable keys.</param>
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
    }
}
