using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Spl;
using LibHac.Util;

namespace LibHac.Common.Keys
{
    public static class ExternalKeyReader
    {
        private const int ReadBufferSize = 1024;

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
            Span<char> buffer = stackalloc char[ReadBufferSize];
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
            Span<char> buffer = stackalloc char[ReadBufferSize];
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
            public bool HasReadEndOfFile;
            public bool SkipNextLine;

            public KvPairReaderContext(TextReader reader, Span<char> buffer)
            {
                Reader = reader;
                Buffer = buffer;
                CurrentKey = default;
                CurrentValue = default;
                BufferPos = buffer.Length;
                NeedFillBuffer = true;
                HasReadEndOfFile = false;
                SkipNextLine = false;
            }
        }

        private enum ReaderStatus
        {
            ReadKey,
            NoKeyRead,
            ReadComment,
            Finished,
            LineTooLong,
            Error
        }

        private enum ReaderState
        {
            Initial,
            Comment,
            Key,
            WhiteSpace1,
            Delimiter,
            Value,
            WhiteSpace2,
            Success,
            CommentSuccess,
            Error
        }

        private static ReaderStatus GetKeyValuePair(ref KvPairReaderContext reader)
        {
            Span<char> buffer = reader.Buffer;

            if (reader.BufferPos == buffer.Length && reader.HasReadEndOfFile)
            {
                // There is no more text to parse. Return that we've finished.
                return ReaderStatus.Finished;
            }

            if (reader.NeedFillBuffer)
            {
                // Move unread text to the front of the buffer
                buffer.Slice(reader.BufferPos).CopyTo(buffer);

                int charsRead = reader.Reader.ReadBlock(buffer.Slice(buffer.Length - reader.BufferPos));

                // ReadBlock will only read less than the buffer size if there's nothing left to read
                if (charsRead != reader.BufferPos)
                {
                    buffer = buffer.Slice(0, buffer.Length - reader.BufferPos + charsRead);
                    reader.Buffer = buffer;
                    reader.HasReadEndOfFile = true;
                }

                reader.NeedFillBuffer = false;
                reader.BufferPos = 0;
            }

            if (reader.SkipNextLine)
            {
                while (reader.BufferPos < buffer.Length && !IsEndOfLine(buffer[reader.BufferPos]))
                {
                    reader.BufferPos++;
                }

                // Stop skipping once we reach a new line
                if (reader.BufferPos < buffer.Length)
                {
                    reader.SkipNextLine = false;
                }
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
                    case ReaderState.Initial when c == '#':
                        state = ReaderState.Comment;
                        keyOffset = i;
                        continue;
                    case ReaderState.Initial when IsEndOfLine(c):
                        // The line was empty. Update the buffer position to indicate a new line
                        reader.BufferPos = i;
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
                        state = ReaderState.Success;
                        valueLength = i - valueOffset;
                        break;
                    case ReaderState.Value when IsWhiteSpace(c):
                        state = ReaderState.WhiteSpace2;
                        valueLength = i - valueOffset;
                        continue;
                    case ReaderState.WhiteSpace2 when IsWhiteSpace(c):
                        continue;
                    case ReaderState.WhiteSpace2 when IsEndOfLine(c):
                        state = ReaderState.Success;
                        break;
                    case ReaderState.Comment when IsEndOfLine(c):
                        keyLength = i - keyOffset;
                        state = ReaderState.CommentSuccess;
                        break;
                    case ReaderState.Comment:
                        continue;

                    // If none of the expected characters were found while in these states, the
                    // line is considered invalid.
                    case ReaderState.Initial:
                    case ReaderState.Key:
                    case ReaderState.WhiteSpace1:
                    case ReaderState.Delimiter:
                        state = ReaderState.Error;
                        continue;
                    case ReaderState.Error when !IsEndOfLine(c):
                        continue;
                }

                // We've exited the state machine for one reason or another
                break;
            }

            // First check if hit the end of the buffer or read the entire buffer without seeing a new line
            if (i == buffer.Length && !reader.HasReadEndOfFile)
            {
                reader.NeedFillBuffer = true;

                // If the entire buffer is part of a single long line
                if (reader.BufferPos == 0 || reader.SkipNextLine)
                {
                    reader.BufferPos = i;

                    // The line might continue past the end of the current buffer, so skip the
                    // remainder of the line after the buffer is refilled.
                    reader.SkipNextLine = true;
                    return ReaderStatus.LineTooLong;
                }

                return ReaderStatus.NoKeyRead;
            }

            // The only way we should exit the loop in the "Value" or "WhiteSpace2" state is if we reached
            // the end of the buffer in that state, meaning i == buffer.Length.
            // Running out of buffer when we haven't read the end of the file will have been handled by the
            // previous "if" block. If we get to this point in those states, we should be at the very end
            // of the file which will be treated as the end of a line.
            if (state == ReaderState.Value || state == ReaderState.WhiteSpace2)
            {
                Assert.SdkEqual(i, buffer.Length);
                Assert.SdkAssert(reader.HasReadEndOfFile);

                // WhiteSpace2 will have already set this value
                if (state == ReaderState.Value)
                    valueLength = i - valueOffset;

                state = ReaderState.Success;
            }

            // Same situation as the two above states
            if (state == ReaderState.Comment)
            {
                Assert.SdkEqual(i, buffer.Length);
                Assert.SdkAssert(reader.HasReadEndOfFile);

                keyLength = i - keyOffset;
                state = ReaderState.CommentSuccess;
            }

            // Same as the above states except the final line was empty or whitespace.
            if (state == ReaderState.Initial)
            {
                Assert.SdkEqual(i, buffer.Length);
                Assert.SdkAssert(reader.HasReadEndOfFile);

                reader.BufferPos = i;
                return ReaderStatus.NoKeyRead;
            }

            // If we successfully read both the key and value
            if (state == ReaderState.Success)
            {
                reader.CurrentKey = reader.Buffer.Slice(keyOffset, keyLength);
                reader.CurrentValue = reader.Buffer.Slice(valueOffset, valueLength);
                reader.BufferPos = i;

                return ReaderStatus.ReadKey;
            }

            if (state == ReaderState.CommentSuccess)
            {
                reader.CurrentKey = reader.Buffer.Slice(keyOffset, keyLength);
                reader.BufferPos = i;

                return ReaderStatus.ReadComment;
            }

            // A bad line was encountered if we're in any of the other states
            // Return the line as "CurrentKey"
            reader.CurrentKey = reader.Buffer.Slice(reader.BufferPos, i - reader.BufferPos);
            reader.BufferPos = i;

            return ReaderStatus.Error;

            static bool IsWhiteSpace(char c) => c == ' ' || c == '\t';
            static bool IsDelimiter(char c) => c == '=' || c == ',';
            static bool IsEndOfLine(char c) => c == '\0' || c == '\r' || c == '\n';

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
            UnsafeHelpers.SkipParamInit(out info);

            for (int i = 0; i < keyList.Count; i++)
            {
                if (keyList[i].Matches(keyName, out int keyIndex, out bool isDev))
                {
                    info = new SpecificKeyInfo(keyList[i], keyIndex, isDev);
                    return true;
                }
            }

            return false;
        }
    }
}
