using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSystem.Impl;
using LibHac.Util;

namespace LibHac.FsSystem
{
    public class PartitionFileSystemMetaCore<T> where T : unmanaged, IPartitionFileSystemEntry
    {
        private static int HeaderSize => Unsafe.SizeOf<Header>();
        private static int EntrySize => Unsafe.SizeOf<T>();

        private bool IsInitialized { get; set; }
        private int EntryCount { get; set; }
        private int StringTableSize { get; set; }
        private int StringTableOffset { get; set; }
        private byte[] Buffer { get; set; }

        public int Size { get; private set; }

        public Result Initialize(IStorage baseStorage)
        {
            var header = new Header();

            Result rc = baseStorage.Read(0, SpanHelpers.AsByteSpan(ref header));
            if (rc.IsFailure()) return rc;

            int pfsMetaSize = HeaderSize + header.EntryCount * EntrySize + header.StringTableSize;
            Buffer = new byte[pfsMetaSize];
            Size = pfsMetaSize;

            return Initialize(baseStorage, Buffer);
        }

        private Result Initialize(IStorage baseStorage, Span<byte> buffer)
        {
            if (buffer.Length < HeaderSize)
                return ResultFs.InvalidSize.Log();

            Result rc = baseStorage.Read(0, buffer.Slice(0, HeaderSize));
            if (rc.IsFailure()) return rc;

            ref Header header = ref Unsafe.As<byte, Header>(ref MemoryMarshal.GetReference(buffer));

            if (header.Magic != GetMagicValue())
                return GetInvalidMagicResult();

            EntryCount = header.EntryCount;

            int entryTableOffset = HeaderSize;
            int entryTableSize = EntryCount * EntrySize;

            StringTableOffset = entryTableOffset + entryTableSize;
            StringTableSize = header.StringTableSize;

            int pfsMetaSize = StringTableOffset + StringTableSize;

            if (buffer.Length < pfsMetaSize)
                return ResultFs.InvalidSize.Log();

            rc = baseStorage.Read(entryTableOffset,
                buffer.Slice(entryTableOffset, entryTableSize + StringTableSize));

            if (rc.IsSuccess())
            {
                IsInitialized = true;
            }

            return rc;
        }

        public int GetEntryCount()
        {
            // FS aborts instead of returning the result value
            if (!IsInitialized)
                throw new HorizonResultException(ResultFs.PreconditionViolation.Log());

            return EntryCount;
        }

        public int FindEntry(U8Span name)
        {
            // FS aborts instead of returning the result value
            if (!IsInitialized)
                throw new HorizonResultException(ResultFs.PreconditionViolation.Log());

            int stringTableSize = StringTableSize;

            ReadOnlySpan<T> entries = GetEntries();
            ReadOnlySpan<byte> names = GetStringTable();

            for (int i = 0; i < entries.Length; i++)
            {
                if (stringTableSize <= entries[i].NameOffset)
                {
                    throw new HorizonResultException(ResultFs.InvalidPartitionEntryOffset.Log());
                }

                ReadOnlySpan<byte> entryName = names.Slice(entries[i].NameOffset);

                if (StringUtils.Compare(name, entryName) == 0)
                {
                    return i;
                }
            }

            return -1;
        }

        public ref T GetEntry(int index)
        {
            if (!IsInitialized || index < 0 || index > EntryCount)
                throw new HorizonResultException(ResultFs.PreconditionViolation.Log());

            return ref GetEntries()[index];
        }

        public U8Span GetName(int index)
        {
            int nameOffset = GetEntry(index).NameOffset;
            ReadOnlySpan<byte> table = GetStringTable();

            // Nintendo doesn't check the offset here like they do in FindEntry, but we will for safety
            if (table.Length <= nameOffset)
            {
                throw new HorizonResultException(ResultFs.InvalidPartitionEntryOffset.Log());
            }

            return new U8Span(table.Slice(nameOffset));
        }

        private Span<T> GetEntries()
        {
            Debug.Assert(IsInitialized);
            Debug.Assert(Buffer.Length >= HeaderSize + EntryCount * EntrySize);

            Span<byte> entryBuffer = Buffer.AsSpan(HeaderSize, EntryCount * EntrySize);
            return MemoryMarshal.Cast<byte, T>(entryBuffer);
        }

        private ReadOnlySpan<byte> GetStringTable()
        {
            Debug.Assert(IsInitialized);
            Debug.Assert(Buffer.Length >= StringTableOffset + StringTableSize);

            return Buffer.AsSpan(StringTableOffset, StringTableSize);
        }

        // You can't attach constant values to interfaces in C#, so workaround that
        // by getting the values based on which generic type is used
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Result GetInvalidMagicResult()
        {
            if (typeof(T) == typeof(StandardEntry))
            {
                return ResultFs.PartitionSignatureVerificationFailed.Log();
            }

            if (typeof(T) == typeof(HashedEntry))
            {
                return ResultFs.Sha256PartitionSignatureVerificationFailed.Log();
            }

            throw new NotSupportedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint GetMagicValue()
        {
            if (typeof(T) == typeof(StandardEntry))
            {
                return 0x30534650; // PFS0
            }

            if (typeof(T) == typeof(HashedEntry))
            {
                return 0x30534648; // HFS0
            }

            throw new NotSupportedException();
        }

        [StructLayout(LayoutKind.Sequential, Size = 0x10)]
        private struct Header
        {
            public uint Magic;
            public int EntryCount;
            public int StringTableSize;
        }
    }
}
