using System;
using System.Buffers;
using System.IO;

namespace LibHac.IO
{
    public static class StorageExtensions
    {
        public static void Read(this IStorage storage, byte[] buffer, long offset, int count, int bufferOffset)
        {
            ValidateStorageParameters(buffer, offset, count, bufferOffset);
            storage.Read(buffer.AsSpan(bufferOffset, count), offset);
        }
        public static void Write(this IStorage storage, byte[] buffer, long offset, int count, int bufferOffset)
        {
            ValidateStorageParameters(buffer, offset, count, bufferOffset);
            storage.Write(buffer.AsSpan(bufferOffset, count), offset);
        }

        private static void ValidateStorageParameters(byte[] buffer, long offset, int count, int bufferOffset)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), "Argument must be non-negative.");
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), "Argument must be non-negative.");
            if (bufferOffset < 0) throw new ArgumentOutOfRangeException(nameof(bufferOffset), "Argument must be non-negative.");
        }

        public static IStorage Slice(this IStorage storage, long start)
        {
            if (storage.Length == -1)
            {
                return storage.Slice(start, storage.Length);
            }

            return storage.Slice(start, storage.Length - start);
        }

        public static IStorage Slice(this IStorage storage, long start, long length)
        {
            return storage.Slice(start, length, true);
        }

        public static IStorage Slice(this IStorage storage, long start, long length, bool leaveOpen)
        {
            return new SubStorage(storage, start, length, leaveOpen);
        }

        public static IStorage AsReadOnly(this IStorage storage)
        {
            return storage.AsReadOnly(true);
        }

        public static IStorage AsReadOnly(this IStorage storage, bool leaveOpen)
        {
            return new SubStorage(storage, 0, storage.Length, leaveOpen, FileAccess.Read);
        }

        public static Stream AsStream(this IStorage storage) => new StorageStream(storage, FileAccess.ReadWrite, true);
        public static Stream AsStream(this IStorage storage, FileAccess access) => new StorageStream(storage, access, true);
        public static Stream AsStream(this IStorage storage, FileAccess access, bool keepOpen) => new StorageStream(storage, access, keepOpen);

        public static IFile AsFile(this IStorage storage, OpenMode mode) => new StorageFile(storage, mode);

        public static void CopyTo(this IStorage input, IStorage output, IProgressReport progress = null)
        {
            const int bufferSize = 81920;
            long remaining = Math.Min(input.Length, output.Length);
            if (remaining < 0) throw new ArgumentException("Storage must have an explicit length");
            progress?.SetTotal(remaining);

            long pos = 0;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                while (remaining > 0)
                {
                    int toCopy = (int)Math.Min(bufferSize, remaining);
                    Span<byte> buf = buffer.AsSpan(0, toCopy);
                    input.Read(buf, pos);
                    output.Write(buf, pos);

                    remaining -= toCopy;
                    pos += toCopy;

                    progress?.ReportAdd(toCopy);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            progress?.SetTotal(0);
        }

        public static void WriteAllBytes(this IStorage input, string filename, IProgressReport progress = null)
        {
            using (var outFile = new FileStream(filename, FileMode.Create, FileAccess.Write))
            {
                input.CopyToStream(outFile, input.Length, progress);
            }
        }

        public static byte[] ToArray(this IStorage storage)
        {
            if (storage == null) return new byte[0];

            var arr = new byte[storage.Length];
            storage.CopyTo(new MemoryStorage(arr));
            return arr;
        }

        public static void CopyToStream(this IStorage input, Stream output, long length, IProgressReport progress = null)
        {
            const int bufferSize = 0x8000;
            long remaining = length;
            long inOffset = 0;
            var buffer = new byte[bufferSize];
            progress?.SetTotal(length);

            while (remaining > 0)
            {
                int toWrite = (int)Math.Min(buffer.Length, remaining);
                input.Read(buffer.AsSpan(0, toWrite), inOffset);

                output.Write(buffer, 0, toWrite);
                remaining -= toWrite;
                inOffset += toWrite;
                progress?.ReportAdd(toWrite);
            }
        }

        public static void CopyToStream(this IStorage input, Stream output) => CopyToStream(input, output, input.Length);

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
