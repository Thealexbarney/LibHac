using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using LibHac.Fs;
using LibHac.Spl;
using LibHac.Util;

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
                using var storage = new FileStream(filename, FileMode.Open, FileAccess.Read);
                ReadMainKeys(keySet, storage, keyInfos, logger);
            }

            if (consoleKeysFilename != null)
            {
                using var storage = new FileStream(consoleKeysFilename, FileMode.Open, FileAccess.Read);
                ReadMainKeys(keySet, storage, keyInfos, logger);
            }

            if (titleKeysFilename != null)
            {
                using var storage = new FileStream(titleKeysFilename, FileMode.Open, FileAccess.Read);
                ReadTitleKeys(keySet, storage, logger);
            }

            keySet.DeriveKeys(logger);

            // Dev keys can be read from prod key files, so derive any missing keys if necessary.
            if (keySet.CurrentMode == KeySet.Mode.Prod)
            {
                keySet.SetMode(KeySet.Mode.Dev);
                keySet.DeriveKeys(logger);
                keySet.SetMode(KeySet.Mode.Prod);
            }
        }

        /// <summary>
        /// Loads keys from key files into an existing <see cref="KeySet"/>. Missing keys will be
        /// derived from existing keys if possible. Any <see langword="null"/> file names will be skipped.
        /// </summary>
        /// <param name="keySet">The <see cref="KeySet"/> where the loaded keys will be placed.</param>
        /// <param name="prodKeysFilename">The path of the file containing common prod keys. Can be <see langword="null"/>.</param>
        /// <param name="devKeysFilename">The path of the file containing common dev keys. Can be <see langword="null"/>.</param>
        /// <param name="titleKeysFilename">The path of the file containing title keys. Can be <see langword="null"/>.</param>
        /// <param name="consoleKeysFilename">The path of the file containing device-unique keys. Can be <see langword="null"/>.</param>
        /// <param name="logger">An optional logger that key-parsing errors will be written to.</param>
        public static void ReadKeyFile(KeySet keySet, string prodKeysFilename = null, string devKeysFilename = null,
            string titleKeysFilename = null, string consoleKeysFilename = null, IProgressReport logger = null)
        {
            KeySet.Mode originalMode = keySet.CurrentMode;
            List<KeyInfo> keyInfos = DefaultKeySet.CreateKeyList();

            if (prodKeysFilename != null)
            {
                keySet.SetMode(KeySet.Mode.Prod);
                using var storage = new FileStream(prodKeysFilename, FileMode.Open, FileAccess.Read);
                ReadMainKeys(keySet, storage, keyInfos, logger);
            }

            if (devKeysFilename != null)
            {
                keySet.SetMode(KeySet.Mode.Dev);
                using var storage = new FileStream(devKeysFilename, FileMode.Open, FileAccess.Read);
                ReadMainKeys(keySet, storage, keyInfos, logger);
            }

            keySet.SetMode(originalMode);

            if (consoleKeysFilename != null)
            {
                using var storage = new FileStream(consoleKeysFilename, FileMode.Open, FileAccess.Read);
                ReadMainKeys(keySet, storage, keyInfos, logger);
            }

            if (titleKeysFilename != null)
            {
                using var storage = new FileStream(titleKeysFilename, FileMode.Open, FileAccess.Read);
                ReadTitleKeys(keySet, storage, logger);
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
        /// <param name="reader">A <see cref="Stream"/> containing the keys to load.</param>
        /// <param name="keyList">A list of all the keys that will be loaded into the key set.
        /// <see cref="DefaultKeySet.CreateKeyList"/> will create a list containing all loadable keys.</param>
        /// <param name="logger">An optional logger that key-parsing errors will be written to.</param>
        public static void ReadMainKeys(KeySet keySet, Stream reader, List<KeyInfo> keyList,
                    IProgressReport logger = null)
        {
            if (reader == null) return;

            using var streamReader = new StreamReader(reader);
            Span<char> buffer = stackalloc char[1024];
            var ctx = new KvPairReaderContext(streamReader, buffer);

            while (true)
            {
                ReaderStatus status = GetKeyValuePair(ref ctx);

                if (status == ReaderStatus.Error)
                {
                    logger?.LogMessage($"Invalid line in key data: \"{ctx.CurrentKey.ToString()}\"");
                }
                else if (status == ReaderStatus.ReadKey)
                {
                    if (!TryGetKeyInfo(out SpecificKeyInfo info, keyList, ctx.CurrentKey))
                    {
                        logger?.LogMessage($"Failed to match key {ctx.CurrentKey.ToString()}");
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

                    if (ctx.CurrentValue.Length != key.Length * 2)
                    {
                        logger?.LogMessage($"Key {ctx.CurrentKey.ToString()} has incorrect size {ctx.CurrentValue.Length}. Must be {key.Length * 2} hex digits.");
                        continue;
                    }

                    if (!StringUtils.TryFromHexString(ctx.CurrentValue, key))
                    {
                        key.Clear();

                        logger?.LogMessage($"Key {ctx.CurrentKey.ToString()} has an invalid value. Must be {key.Length * 2} hex digits.");
                    }
                }
                else if (status == ReaderStatus.Finished)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Loads title keys from a <see cref="TextReader"/> into an existing <see cref="KeySet"/>.
        /// </summary>
        /// <param name="keySet">The <see cref="KeySet"/> where the loaded keys will be placed.</param>
        /// <param name="reader">A <see cref="Stream"/> containing the keys to load.</param>
        /// <param name="logger">An optional logger that key-parsing errors will be written to.</param>
        public static void ReadTitleKeys(KeySet keySet, Stream reader, IProgressReport logger = null)
        {
            if (reader == null) return;

            using var streamReader = new StreamReader(reader);
            Span<char> buffer = stackalloc char[1024];
            var ctx = new KvPairReaderContext(streamReader, buffer);

            // Estimate the number of keys by assuming each line is about 69 bytes.
            // Subtract 2 from that so we estimate slightly high. 
            keySet.ExternalKeySet.EnsureCapacity((int)reader.Length / 67);

            while (true)
            {
                ReaderStatus status = GetKeyValuePair(ref ctx);

                if (status == ReaderStatus.Error)
                {
                    logger?.LogMessage($"Invalid line in key data: \"{ctx.CurrentKey.ToString()}\"");
                    Debugger.Break();
                }
                else if (status == ReaderStatus.ReadKey)
                {
                    if (ctx.CurrentKey.Length != TitleKeySize * 2)
                    {
                        logger?.LogMessage($"Rights ID {ctx.CurrentKey.ToString()} has incorrect size {ctx.CurrentKey.Length}. (Expected {TitleKeySize * 2})");
                        continue;
                    }

                    if (ctx.CurrentValue.Length != TitleKeySize * 2)
                    {
                        logger?.LogMessage($"Title key {ctx.CurrentValue.ToString()} has incorrect size {ctx.CurrentValue.Length}. (Expected {TitleKeySize * 2})");
                        continue;
                    }

                    var rightsId = new RightsId();
                    var titleKey = new AccessKey();

                    if (!StringUtils.TryFromHexString(ctx.CurrentKey, SpanHelpers.AsByteSpan(ref rightsId)))
                    {
                        logger?.LogMessage($"Invalid rights ID \"{ctx.CurrentKey.ToString()}\" in title key file");
                        continue;
                    }

                    if (!StringUtils.TryFromHexString(ctx.CurrentValue, SpanHelpers.AsByteSpan(ref titleKey)))
                    {
                        logger?.LogMessage($"Invalid title key \"{ctx.CurrentValue.ToString()}\" in title key file");
                        continue;
                    }

                    keySet.ExternalKeySet.Add(rightsId, titleKey).ThrowIfFailure();
                }
                else if (status == ReaderStatus.Finished)
                {
                    break;
                }
            }
        }

        private ref struct KvPairReaderContext
        {
            public TextReader Reader;
            public Span<char> Buffer;
            public Span<char> CurrentKey;
            public Span<char> CurrentValue;
            public int BufferPos;
            public bool NeedFillBuffer;

            public KvPairReaderContext(TextReader reader, Span<char> buffer)
            {
                Reader = reader;
                Buffer = buffer;
                CurrentKey = default;
                CurrentValue = default;
                BufferPos = buffer.Length;
                NeedFillBuffer = true;
            }
        }

        private enum ReaderStatus
        {
            ReadKey,
            NoKeyRead,
            Finished,
            Error
        }

        private enum ReaderState
        {
            Initial,
            Key,
            WhiteSpace1,
            Delimiter,
            Value,
            WhiteSpace2,
            End
        }

        private static ReaderStatus GetKeyValuePair(ref KvPairReaderContext reader)
        {
            Span<char> buffer = reader.Buffer;

            if (reader.NeedFillBuffer)
            {
                // Move unread text to the front of the buffer
                buffer.Slice(reader.BufferPos).CopyTo(buffer);

                int charsRead = reader.Reader.ReadBlock(buffer.Slice(buffer.Length - reader.BufferPos));

                if (charsRead == 0)
                {
                    return ReaderStatus.Finished;
                }

                // ReadBlock will only read less than the buffer size if there's nothing left to read
                if (charsRead != reader.BufferPos)
                {
                    buffer = buffer.Slice(0, buffer.Length - reader.BufferPos + charsRead);
                    reader.Buffer = buffer;
                }

                reader.NeedFillBuffer = false;
                reader.BufferPos = 0;
            }

            // Skip any empty lines
            while (reader.BufferPos < buffer.Length && IsEndOfLine(buffer[reader.BufferPos]))
            {
                reader.BufferPos++;
            }

            var state = ReaderState.Initial;
            int keyOffset = -1;
            int keyLength = -1;
            int valueOffset = -1;
            int valueLength = -1;
            int i;

            for (i = reader.BufferPos; i < buffer.Length; i++)
            {
                char c = buffer[i];

                switch (state)
                {
                    case ReaderState.Initial when IsWhiteSpace(c):
                        continue;
                    case ReaderState.Initial when IsValidNameChar(c):
                        state = ReaderState.Key;
                        keyOffset = i;

                        // Skip the next few rounds through the state machine since we know we should be 
                        // encountering a string of name characters
                        do
                        {
                            ToLower(ref buffer[i]);
                            i++;
                        } while (i < buffer.Length && IsValidNameChar(buffer[i]));

                        // Decrement so we can process this character the next round through the state machine
                        i--;
                        continue;
                    case ReaderState.Key when IsWhiteSpace(c):
                        state = ReaderState.WhiteSpace1;
                        keyLength = i - keyOffset;
                        continue;
                    case ReaderState.Key when IsDelimiter(c):
                        state = ReaderState.Delimiter;
                        keyLength = i - keyOffset;
                        continue;
                    case ReaderState.WhiteSpace1 when IsWhiteSpace(c):
                        continue;
                    case ReaderState.WhiteSpace1 when IsDelimiter(c):
                        state = ReaderState.Delimiter;
                        continue;
                    case ReaderState.Delimiter when IsWhiteSpace(c):
                        continue;
                    case ReaderState.Delimiter when StringUtils.IsHexDigit((byte)c):
                        state = ReaderState.Value;
                        valueOffset = i;

                        do
                        {
                            i++;
                        } while (i < buffer.Length && !IsEndOfLine(buffer[i]) && !IsWhiteSpace(buffer[i]));

                        i--;
                        continue;
                    case ReaderState.Value when IsEndOfLine(c):
                        state = ReaderState.End;
                        valueLength = i - valueOffset;
                        continue;
                    case ReaderState.Value when IsWhiteSpace(c):
                        state = ReaderState.WhiteSpace2;
                        valueLength = i - valueOffset;
                        continue;
                    case ReaderState.WhiteSpace2 when IsWhiteSpace(c):
                        continue;
                    case ReaderState.WhiteSpace2 when IsEndOfLine(c):
                        state = ReaderState.End;
                        continue;
                    case ReaderState.End when IsEndOfLine(c):
                        continue;
                    case ReaderState.End when !IsEndOfLine(c):
                        break;
                }

                // We've exited the state machine for one reason or another
                break;
            }

            // If we successfully read both the key and value
            if (state == ReaderState.End || state == ReaderState.WhiteSpace2)
            {
                reader.CurrentKey = reader.Buffer.Slice(keyOffset, keyLength);
                reader.CurrentValue = reader.Buffer.Slice(valueOffset, valueLength);
                reader.BufferPos = i;

                return ReaderStatus.ReadKey;
            }

            // We either ran out of buffer or hit an error reading the key-value pair.
            // Advance to the end of the line if possible.
            while (i < buffer.Length && !IsEndOfLine(buffer[i]))
            {
                i++;
            }

            // We don't have a complete line. Return that the buffer needs to be refilled.
            if (i == buffer.Length)
            {
                reader.NeedFillBuffer = true;
                return ReaderStatus.NoKeyRead;
            }

            // If we hit a line with an error, it'll be returned as "CurrentKey" in the reader context
            reader.CurrentKey = buffer.Slice(reader.BufferPos, i - reader.BufferPos);
            reader.BufferPos = i;

            return ReaderStatus.Error;

            static bool IsWhiteSpace(char c)
            {
                return c == ' ' || c == '\t';
            }

            static bool IsDelimiter(char c)
            {
                return c == '=' || c == ',';
            }

            static bool IsEndOfLine(char c)
            {
                return c == '\0' || c == '\r' || c == '\n';
            }

            static void ToLower(ref char c)
            {
                // The only characters we need to worry about are underscores and alphanumerics
                // Both lowercase and numbers have bit 5 set, so they're both treated the same
                if (c != '_')
                {
                    c |= (char)0b100000;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsValidNameChar(char c)
        {
            return (c | 0x20u) - 'a' <= 'z' - 'a' || (uint)(c - '0') <= 9 || c == '_';
        }

        private static bool TryGetKeyInfo(out SpecificKeyInfo info, List<KeyInfo> keyList, ReadOnlySpan<char> keyName)
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
