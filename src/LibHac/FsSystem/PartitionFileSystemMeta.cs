using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Crypto;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.FsSystem.Impl;
using LibHac.Mem;
using LibHac.Util;
using Buffer = LibHac.Mem.Buffer;

namespace LibHac.FsSystem
{
    /// <summary>
    /// Contains values used by <see cref="PartitionFileSystemMetaCore{TFormat,THeader,TEntry}"/> for reading
    /// and building the metadata of a partition file system.
    /// </summary>
    public interface IPartitionFileSystemFormat
    {
        /// <summary>The signature bytes that are expected to be at the start of the partition file system.</summary>
        static abstract ReadOnlySpan<byte> VersionSignature { get; }

        /// <summary>The maximum length of file names inside the partition file system.</summary>
        static abstract uint EntryNameLengthMax { get; }

        /// <summary>The alignment that the start of the data for each file must be aligned to.</summary>
        static abstract uint FileDataAlignmentSize { get; }

        /// <summary>The <see cref="Result"/> returned when the <see cref="VersionSignature"/> is incorrect.</summary>
        static abstract Result ResultSignatureVerificationFailed { get; }
    }

    /// <summary>
    /// The minimum fields needed for the file entry type in a <see cref="PartitionFileSystemMetaCore{TFormat,THeader,TEntry}"/>.
    /// </summary>
    public interface IPartitionFileSystemEntry
    {
        long Offset { get; }
        long Size { get; }
        int NameOffset { get; }
    }

    /// <summary>
    /// The minimum fields needed for the header type in a <see cref="PartitionFileSystemMetaCore{TFormat,THeader,TEntry}"/>.
    /// </summary>
    public interface IPartitionFileSystemHeader
    {
        ReadOnlySpan<byte> Signature { get; }
        int EntryCount { get; }
        int NameTableSize { get; }
    }

    /// <summary>
    /// Reads the metadata from a <see cref="PartitionFileSystemCore{TMetaData,TFormat,THeader,TEntry}"/>.
    /// The metadata has three sections: A single struct of type <typeparamref name="TFormat"/>, a table of
    /// <typeparamref name="TEntry"/> structs containing info on each file, and a table of the names of all the files.
    /// </summary>
    /// <typeparam name="TFormat">A traits class that provides values used to read and build the metadata.</typeparam>
    /// <typeparam name="THeader">The type of the header at the beginning of the metadata.</typeparam>
    /// <typeparam name="TEntry">The type of the entries in the file table in the metadata.</typeparam>
    /// <remarks>Based on nnSdk 16.2.0 (FS 16.0.0)</remarks>
    public class PartitionFileSystemMetaCore<TFormat, THeader, TEntry> : IDisposable
        where TFormat : IPartitionFileSystemFormat
        where THeader : unmanaged, IPartitionFileSystemHeader
        where TEntry : unmanaged, IPartitionFileSystemEntry
    {
        protected bool IsInitialized;
        protected BufferSegment HeaderBuffer;
        protected BufferSegment EntryBuffer;
        protected BufferSegment NameTableBuffer;
        protected long MetaDataSize;
        protected MemoryResource Allocator;
        protected Buffer MetaDataBuffer;

        private ref readonly THeader Header => ref MemoryMarshal.GetReference(HeaderBuffer.GetSpan<THeader>());
        private ReadOnlySpan<TEntry> Entries => EntryBuffer.GetSpan<TEntry>();
        private ReadOnlySpan<byte> NameTable => NameTableBuffer.Span;

        public PartitionFileSystemMetaCore()
        {
            IsInitialized = false;
            MetaDataSize = 0;
            Allocator = null;
            MetaDataBuffer = default;
        }

        public virtual void Dispose()
        {
            DeallocateBuffer();
        }

        protected void DeallocateBuffer()
        {
            if (!MetaDataBuffer.IsNull)
            {
                Assert.SdkNotNull(Allocator);

                Allocator.Deallocate(ref MetaDataBuffer);
            }
        }

        public Result Initialize(IStorage baseStorage, Buffer metaBuffer, int metaDataSize)
        {
            // Added check for LibHac because Buffer carries a length along with its pointer.
            if (metaBuffer.Length < metaDataSize)
                return ResultFs.InvalidSize.Log();

            Span<byte> metaSpan = metaBuffer.Span.Slice(0, metaDataSize);

            // Validate size for header.
            if (metaDataSize < Unsafe.SizeOf<THeader>())
                return ResultFs.InvalidSize.Log();

            // Read the header.
            Result res = baseStorage.Read(offset: 0, metaSpan);
            if (res.IsFailure()) return res.Miss();

            // Set and validate the header.
            // Get the section of the buffer that contains the header.
            HeaderBuffer = metaBuffer.GetSegment(0, Unsafe.SizeOf<THeader>());
            Span<byte> headerSpan = HeaderBuffer.Span;
            ref readonly THeader header = ref Unsafe.As<byte, THeader>(ref MemoryMarshal.GetReference(headerSpan));

            if (!CryptoUtil.IsSameBytes(headerSpan, TFormat.VersionSignature, TFormat.VersionSignature.Length))
                return ResultFs.PartitionSignatureVerificationFailed.Log();

            res = QueryMetaDataSize(out MetaDataSize, in header);
            if (res.IsFailure()) return res.Miss();

            int entriesSize = header.EntryCount * Unsafe.SizeOf<TEntry>();

            // Note: Instead of doing this check after assigning the buffers like in the original, we do the check before
            // assigning the buffers because trying to get the buffers when the meta buffer is too small will
            // result in an exception in C#.

            // Validate size for header + entries + name table.
            if (metaDataSize < Unsafe.SizeOf<THeader>() + entriesSize + header.NameTableSize)
                return ResultFs.InvalidSize.Log();

            // Setup entries and name table.
            EntryBuffer = metaBuffer.GetSegment(Unsafe.SizeOf<THeader>(), entriesSize);
            NameTableBuffer = metaBuffer.GetSegment(Unsafe.SizeOf<THeader>() + entriesSize, header.NameTableSize);

            // Read entries and name table.
            Span<byte> destSpan = metaSpan.Slice(Unsafe.SizeOf<THeader>(), entriesSize + header.NameTableSize);
            res = baseStorage.Read(Unsafe.SizeOf<THeader>(), destSpan);
            if (res.IsFailure()) return res.Miss();

            // Mark as initialized.
            IsInitialized = true;
            return Result.Success;
        }

        public Result Initialize(IStorage baseStorage, MemoryResource allocator)
        {
            Assert.SdkRequiresNotNull(allocator);

            // Determine the meta data size.
            Result res = QueryMetaDataSize(out MetaDataSize, baseStorage);
            if (res.IsFailure()) return res.Miss();

            // Deallocate any old meta buffer and allocate a new one.
            DeallocateBuffer();
            Allocator = allocator;
            MetaDataBuffer = Allocator.Allocate(MetaDataSize);
            if (MetaDataBuffer.IsNull)
                return ResultFs.AllocationMemoryFailedInPartitionFileSystemMetaA.Log();

            // Perform regular initialization.
            res = Initialize(baseStorage, MetaDataBuffer, (int)MetaDataSize);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }

        /// <summary>
        /// Queries the size of the metadata by reading the metadata header from the provided storage
        /// </summary>
        /// <param name="outSize">If the operation returns successfully, contains the size of the metadata.</param>
        /// <param name="storage">The <see cref="IStorage"/> containing the metadata.</param>
        /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
        /// <see cref="IPartitionFileSystemFormat.ResultSignatureVerificationFailed"/>: The header doesn't have
        /// the correct file signature.</returns>
        public static Result QueryMetaDataSize(out long outSize, IStorage storage)
        {
            UnsafeHelpers.SkipParamInit(out outSize);

            Unsafe.SkipInit(out THeader header);
            Result res = storage.Read(0, SpanHelpers.AsByteSpan(ref header));
            if (res.IsFailure()) return res.Miss();

            res = QueryMetaDataSize(out outSize, in header);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }

        /// <summary>
        /// Queries the size of the metadata with the provided header.
        /// </summary>
        /// <param name="outSize">If the operation returns successfully, contains the size of the metadata.</param>
        /// <param name="header">The metadata header.</param>
        /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
        /// <see cref="IPartitionFileSystemFormat.ResultSignatureVerificationFailed"/>: The header doesn't have
        /// the correct file signature.</returns>
        protected static Result QueryMetaDataSize(out long outSize, in THeader header)
        {
            UnsafeHelpers.SkipParamInit(out outSize);

            if (!CryptoUtil.IsSameBytes(SpanHelpers.AsReadOnlyByteSpan(in header), TFormat.VersionSignature,
                TFormat.VersionSignature.Length))
            {
                return TFormat.ResultSignatureVerificationFailed.Log();
            }

            outSize = Unsafe.SizeOf<THeader>() + header.EntryCount * Unsafe.SizeOf<TEntry>() + header.NameTableSize;
            return Result.Success;
        }

        /// <summary>
        /// Returns the size of the meta data header.
        /// </summary>
        /// <returns>The size of <typeparamref name="THeader"/>.</returns>
        public static int GetHeaderSize()
        {
            return Unsafe.SizeOf<THeader>();
        }

        public int GetMetaDataSize()
        {
            return (int)MetaDataSize;
        }

        public int GetEntryIndex(U8Span entryName)
        {
            if (!IsInitialized)
                return Result.ConvertResultToReturnType<int>(ResultFs.PreconditionViolation.Value);

            ref readonly THeader header = ref Header;
            ReadOnlySpan<TEntry> entries = Entries;
            ReadOnlySpan<byte> nameTable = NameTable;

            for (int i = 0; i < header.EntryCount; i++)
            {
                if (entries[i].NameOffset >= header.NameTableSize)
                    return Result.ConvertResultToReturnType<int>(ResultFs.InvalidPartitionEntryOffset.Value);

                int maxNameLen = header.NameTableSize - entries[i].NameOffset;
                if (StringUtils.Compare(nameTable.Slice(entries[i].NameOffset), entryName, maxNameLen) == 0)
                {
                    return i;
                }
            }

            return -1;
        }

        public ref readonly TEntry GetEntry(int entryIndex)
        {
            Abort.DoAbortUnless(IsInitialized, ResultFs.PreconditionViolation.Value);
            Abort.DoAbortUnless(entryIndex >= 0 && entryIndex < Header.EntryCount, ResultFs.PreconditionViolation.Value);

            return ref Entries[entryIndex];
        }

        public int GetEntryCount()
        {
            if (!IsInitialized)
                return Result.ConvertResultToReturnType<int>(ResultFs.PreconditionViolation.Value);

            return Header.EntryCount;
        }

        public U8Span GetEntryName(int entryIndex)
        {
            Abort.DoAbortUnless(IsInitialized, ResultFs.PreconditionViolation.Value);
            Abort.DoAbortUnless(entryIndex < Header.EntryCount, ResultFs.PreconditionViolation.Value);

            return new U8Span(NameTable.Slice(GetEntry(entryIndex).NameOffset));
        }
    }
}

namespace LibHac.FsSystem
{
    using TFormat = Sha256PartitionFileSystemFormat;
    using THeader = PartitionFileSystemFormat.PartitionFileSystemHeaderImpl;

    /// <summary>
    /// Reads the metadata for a <see cref="Sha256PartitionFileSystem"/>.
    /// </summary>
    /// <remarks>Based on nnSdk 16.2.0 (FS 16.0.0)</remarks>
    public class Sha256PartitionFileSystemMeta : PartitionFileSystemMetaCore<TFormat, THeader, TFormat.PartitionEntry>
    {
        public Result Initialize(IStorage baseStorage, MemoryResource allocator, ReadOnlySpan<byte> hash)
        {
            Result res = Initialize(baseStorage, allocator, hash, salt: default);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }

        public Result Initialize(IStorage baseStorage, MemoryResource allocator, ReadOnlySpan<byte> hash, Optional<byte> salt)
        {
            if (hash.Length != Sha256Generator.HashSize)
                return ResultFs.PreconditionViolation.Log();

            Result res = QueryMetaDataSize(out MetaDataSize, baseStorage);
            if (res.IsFailure()) return res.Miss();

            DeallocateBuffer();
            Allocator = allocator;
            MetaDataBuffer = Allocator.Allocate(MetaDataSize);
            if (MetaDataBuffer.IsNull)
                return ResultFs.AllocationMemoryFailedInPartitionFileSystemMetaB.Log();

            Span<byte> metaDataSpan = MetaDataBuffer.Span.Slice(0, (int)MetaDataSize);

            res = baseStorage.Read(offset: 0, metaDataSpan);
            if (res.IsFailure()) return res.Miss();

            Span<byte> hashBuffer = stackalloc byte[Sha256Generator.HashSize];
            var generator = new Sha256Generator();
            generator.Initialize();
            generator.Update(metaDataSpan);
            if (salt.HasValue)
            {
                generator.Update(SpanHelpers.AsReadOnlyByteSpan(in salt.ValueRo));
            }

            generator.GetHash(hashBuffer);

            if (!CryptoUtil.IsSameBytes(hash, hashBuffer, hash.Length))
                return ResultFs.Sha256PartitionHashVerificationFailed.Log();

            HeaderBuffer = MetaDataBuffer.GetSegment(0, Unsafe.SizeOf<THeader>());
            Span<byte> headerSpan = HeaderBuffer.Span;
            ref readonly THeader header = ref Unsafe.As<byte, THeader>(ref MemoryMarshal.GetReference(headerSpan));

            if (!CryptoUtil.IsSameBytes(headerSpan, TFormat.VersionSignature, TFormat.VersionSignature.Length))
                return TFormat.ResultSignatureVerificationFailed.Log();

            int entriesSize = header.EntryCount * Unsafe.SizeOf<TFormat.PartitionEntry>();

            // Validate size for header + entries + name table.
            if (MetaDataSize < Unsafe.SizeOf<THeader>() + entriesSize + header.NameTableSize)
                return ResultFs.InvalidSha256PartitionMetaDataSize.Log();

            // Setup entries and name table.
            EntryBuffer = MetaDataBuffer.GetSegment(Unsafe.SizeOf<THeader>(), entriesSize);
            NameTableBuffer = MetaDataBuffer.GetSegment(Unsafe.SizeOf<THeader>() + entriesSize, header.NameTableSize);

            // Mark as initialized.
            IsInitialized = true;
            return Result.Success;
        }
    }

    /// <summary>
    /// Reads the metadata for a <see cref="PartitionFileSystem"/>.
    /// </summary>
    /// <remarks>Based on nnSdk 16.2.0 (FS 16.0.0)</remarks>
    public class PartitionFileSystemMeta : PartitionFileSystemMetaCore<PartitionFileSystemFormat,
        PartitionFileSystemFormat.PartitionFileSystemHeaderImpl, PartitionFileSystemFormat.PartitionEntry> { }
}