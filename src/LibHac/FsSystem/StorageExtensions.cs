using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem
{
    public static class StorageExtensions
    {
        [Obsolete("The standard Span-based IStorage methods should be used instead.")]
        public static Result Read(this IStorage storage, long offset, byte[] buffer, int count, int bufferOffset)
        {
            ValidateStorageParameters(buffer, offset, count, bufferOffset);
            return storage.Read(offset, buffer.AsSpan(bufferOffset, count));
        }

        [Obsolete("The standard Span-based IStorage methods should be used instead.")]
        public static Result Write(this IStorage storage, long offset, byte[] buffer, int count, int bufferOffset)
        {
            ValidateStorageParameters(buffer, offset, count, bufferOffset);
            return storage.Write(offset, buffer.AsSpan(bufferOffset, count));
        }

        // todo: remove this method when the above Read and Write methods are removed
        // (Hopefully they're still above, or else someone didn't listen)
        // ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local
        private static void ValidateStorageParameters(byte[] buffer, long offset, int count, int bufferOffset)
        // ReSharper restore ParameterOnlyUsedForPreconditionCheck.Local
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), "Argument must be non-negative.");
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), "Argument must be non-negative.");
            if (bufferOffset < 0) throw new ArgumentOutOfRangeException(nameof(bufferOffset), "Argument must be non-negative.");
        }

        public static IStorage Slice(this IStorage storage, long start)
        {
            storage.GetSize(out long length).ThrowIfFailure();

            if (length == -1)
            {
                return storage.Slice(start, length);
            }

            return storage.Slice(start, length - start);
        }

        public static IStorage Slice(this IStorage storage, long start, long length)
        {
            return storage.Slice(start, length, true);
        }

        public static IStorage Slice(this IStorage storage, long start, long length, bool leaveOpen)
        {
            if (!leaveOpen)
            {
                return new SubStorage(storage, start, length);
            }

            using (var sharedStorage = new ReferenceCountedDisposable<IStorage>(storage))
            {
                return new SubStorage(sharedStorage, start, length);
            }
        }

        public static Stream AsStream(this IStorage storage) => new StorageStream(storage, FileAccess.ReadWrite, true);
        public static Stream AsStream(this IStorage storage, FileAccess access) => new StorageStream(storage, access, true);
        public static Stream AsStream(this IStorage storage, FileAccess access, bool keepOpen) => new StorageStream(storage, access, keepOpen);

        public static IFile AsFile(this IStorage storage, OpenMode mode) => new StorageFile(storage, mode);

        public static void CopyTo(this IStorage input, IStorage output, IProgressReport progress = null, int bufferSize = 81920)
        {
            input.GetSize(out long inputSize).ThrowIfFailure();
            output.GetSize(out long outputSize).ThrowIfFailure();

            long remaining = Math.Min(inputSize, outputSize);
            if (remaining < 0) throw new ArgumentException("Storage must have an explicit length");
            progress?.SetTotal(remaining);

            long pos = 0;

            using var buffer = new RentedArray<byte>(bufferSize);
            int rentedBufferSize = buffer.Array.Length;

            while (remaining > 0)
            {
                int toCopy = (int)Math.Min(rentedBufferSize, remaining);
                Span<byte> buf = buffer.Array.AsSpan(0, toCopy);
                input.Read(pos, buf);
                output.Write(pos, buf);

                remaining -= toCopy;
                pos += toCopy;

                progress?.ReportAdd(toCopy);
            }

            progress?.SetTotal(0);
        }

        public static void Fill(this IStorage input, byte value, IProgressReport progress = null)
        {
            input.GetSize(out long inputSize).ThrowIfFailure();
            input.Fill(value, 0, inputSize, progress);
        }

        public static void Fill(this IStorage input, byte value, long offset, long count, IProgressReport progress = null)
        {
            const int threshold = 0x400;

            if (count > threshold)
            {
                input.FillLarge(value, offset, count, progress);
                return;
            }

            Span<byte> buf = stackalloc byte[(int)count];
            buf.Fill(value);

            input.Write(offset, buf);
        }

        private static void FillLarge(this IStorage input, byte value, long offset, long count, IProgressReport progress = null)
        {
            const int bufferSize = 0x4000;

            long remaining = count;
            if (remaining < 0) throw new ArgumentException("Storage must have an explicit length");
            progress?.SetTotal(remaining);

            long pos = offset;

            using var buffer = new RentedArray<byte>(bufferSize);
            int rentedBufferSize = buffer.Array.Length;

            buffer.Array.AsSpan(0, (int)Math.Min(remaining, rentedBufferSize)).Fill(value);

            while (remaining > 0)
            {
                int toFill = (int)Math.Min(rentedBufferSize, remaining);
                Span<byte> buf = buffer.Array.AsSpan(0, toFill);

                input.Write(pos, buf);

                remaining -= toFill;
                pos += toFill;

                progress?.ReportAdd(toFill);
            }

            progress?.SetTotal(0);
        }

        public static void WriteAllBytes(this IStorage input, string filename, IProgressReport progress = null)
        {
            input.GetSize(out long inputSize).ThrowIfFailure();

            using (var outFile = new FileStream(filename, FileMode.Create, FileAccess.Write))
            {
                input.CopyToStream(outFile, inputSize, progress);
            }
        }

        public static byte[] ToArray(this IStorage storage)
        {
            if (storage == null) return new byte[0];

            storage.GetSize(out long storageSize).ThrowIfFailure();

            byte[] arr = new byte[storageSize];
            storage.CopyTo(new MemoryStorage(arr));
            return arr;
        }

        public static T[] ToArray<T>(this IStorage storage) where T : unmanaged
        {
            if (storage == null) return new T[0];

            storage.GetSize(out long storageSize).ThrowIfFailure();

            var arr = new T[storageSize / Unsafe.SizeOf<T>()];
            Span<byte> dest = MemoryMarshal.Cast<T, byte>(arr.AsSpan());

            storage.Read(0, dest);
            return arr;
        }

        public static void CopyToStream(this IStorage input, Stream output, long length, IProgressReport progress = null, int bufferSize = 0x8000)
        {
            long remaining = length;
            long inOffset = 0;

            using var buffer = new RentedArray<byte>(bufferSize);
            int rentedBufferSize = buffer.Array.Length;

            progress?.SetTotal(length);

            while (remaining > 0)
            {
                int toWrite = (int)Math.Min(rentedBufferSize, remaining);
                input.Read(inOffset, buffer.Array.AsSpan(0, toWrite));

                output.Write(buffer.Array, 0, toWrite);
                remaining -= toWrite;
                inOffset += toWrite;
                progress?.ReportAdd(toWrite);
            }
        }

        public static void CopyToStream(this IStorage input, Stream output, int bufferSize = 0x8000)
        {
            input.GetSize(out long inputSize).ThrowIfFailure();
            CopyToStream(input, output, inputSize, bufferSize: bufferSize);
        }

        public static IStorage AsStorage(this Stream stream)
        {
            if (stream == null) return null;
            return new StreamStorage(stream, true);
        }

        public static IStorage AsStorage(this Stream stream, bool keepOpen)
        {
            if (stream == null) return null;
            return new StreamStorage(stream, keepOpen);
        }

        public static IStorage AsStorage(this Stream stream, long start)
        {
            if (stream == null) return null;
            return new StreamStorage(stream, true).Slice(start);
        }

        public static IStorage AsStorage(this Stream stream, long start, int length)
        {
            if (stream == null) return null;
            return new StreamStorage(stream, true).Slice(start, length);
        }

        public static IStorage AsStorage(this Stream stream, long start, int length, bool keepOpen)
        {
            if (stream == null) return null;
            return new StreamStorage(stream, keepOpen).Slice(start, length);
        }
    }
}
