using System;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace LibHac
{
    public static class Utilities
    {
        private const int MediaSize = 0x200;

        public static byte[][] CreateJaggedByteArray(int len1, int len2)
        {
            var array = new byte[len1][];

            for (int i = 0; i < array.Length; i++)
            {
                array[i] = new byte[len2];
            }

            return array;
        }

        public static byte[][][] CreateJaggedByteArray(int len1, int len2, int len3)
        {
            var array = new byte[len1][][];

            for (int i = 0; i < array.Length; i++)
            {
                array[i] = new byte[len2][];

                for (int j = 0; j < array[i].Length; j++)
                {
                    array[i][j] = new byte[len3];
                }
            }

            return array;
        }

        public static bool ArraysEqual<T>(T[] a1, T[] a2)
        {
            if (a1 == null || a2 == null) return false;
            if (a1 == a2) return true;
            if (a1.Length != a2.Length) return false;

            for (int i = 0; i < a1.Length; i++)
            {
                if (!a1[i].Equals(a2[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool SpansEqual<T>(Span<T> a1, Span<T> a2) where T : IEquatable<T>
        {
            return a1.SequenceEqual(a2);
        }

        public static bool SpansEqual<T>(ReadOnlySpan<T> a1, ReadOnlySpan<T> a2) where T : IEquatable<T>
        {
            return a1.SequenceEqual(a2);
        }

        public static bool IsEmpty(this byte[] array) => ((ReadOnlySpan<byte>)array).IsEmpty();
        public static bool IsEmpty(this Span<byte> span) => ((ReadOnlySpan<byte>)span).IsEmpty();

        public static bool IsEmpty(this ReadOnlySpan<byte> span)
        {
            for (int i = 0; i < span.Length; i++)
            {
                if (span[i] != 0)
                {
                    return false;
                }
            }

            return true;
        }

        public static void XorArrays(Span<byte> transformData, ReadOnlySpan<byte> xorData)
        {
            int sisdStart = 0;
            if (Vector.IsHardwareAccelerated)
            {
                Span<Vector<byte>> dataVec = MemoryMarshal.Cast<byte, Vector<byte>>(transformData);
                ReadOnlySpan<Vector<byte>> xorVec = MemoryMarshal.Cast<byte, Vector<byte>>(xorData);
                sisdStart = dataVec.Length * Vector<byte>.Count;

                for (int i = 0; i < dataVec.Length; i++)
                {
                    dataVec[i] ^= xorVec[i];
                }
            }

            for (int i = sisdStart; i < transformData.Length; i++)
            {
                transformData[i] ^= xorData[i];
            }
        }

        public static void XorArrays(Span<byte> output, ReadOnlySpan<byte> input1, ReadOnlySpan<byte> input2)
        {
            int length = Math.Min(input1.Length, input2.Length);

            int sisdStart = 0;
            if (Vector.IsHardwareAccelerated)
            {
                int lengthVec = length / Vector<byte>.Count;

                Span<Vector<byte>> outputVec = MemoryMarshal.Cast<byte, Vector<byte>>(output);
                ReadOnlySpan<Vector<byte>> input1Vec = MemoryMarshal.Cast<byte, Vector<byte>>(input1);
                ReadOnlySpan<Vector<byte>> input2Vec = MemoryMarshal.Cast<byte, Vector<byte>>(input2);

                sisdStart = lengthVec * Vector<byte>.Count;

                for (int i = 0; i < lengthVec; i++)
                {
                    outputVec[i] = input1Vec[i] ^ input2Vec[i];
                }
            }

            for (int i = sisdStart; i < length; i++)
            {
                output[i] = (byte)(input1[i] ^ input2[i]);
            }
        }

        public static void CopyStream(this Stream input, Stream output, long length, IProgressReport progress = null)
        {
            const int bufferSize = 0x8000;
            long remaining = length;
            var buffer = new byte[bufferSize];
            progress?.SetTotal(length);

            int read;
            while ((read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining))) > 0)
            {
                output.Write(buffer, 0, read);
                remaining -= read;
                progress?.ReportAdd(read);
            }
        }

        public static void WriteAllBytes(this Stream input, string filename, IProgressReport progress = null)
        {
            input.Position = 0;

            using (var outFile = new FileStream(filename, FileMode.Create, FileAccess.Write))
            {
                input.CopyStream(outFile, input.Length, progress);
            }
        }

        public static string ReadAsciiZ(this BinaryReader reader, int maxLength = Int32.MaxValue)
        {
            long start = reader.BaseStream.Position;
            int size = 0;

            // Read until we hit the end of the stream (-1) or a zero
            while (reader.BaseStream.ReadByte() - 1 > 0 && size < maxLength)
            {
                size++;
            }

            reader.BaseStream.Position = start;
            string text = reader.ReadAscii(size);
            reader.BaseStream.Position++; // Skip the null byte
            return text;
        }

        public static string ReadUtf8Z(this BinaryReader reader, int maxLength = int.MaxValue)
        {
            long start = reader.BaseStream.Position;
            int size = 0;

            // Read until we hit the end of the stream (-1) or a zero
            while (reader.BaseStream.ReadByte() - 1 > 0 && size < maxLength)
            {
                size++;
            }

            reader.BaseStream.Position = start;
            string text = reader.ReadUtf8(size);
            reader.BaseStream.Position++; // Skip the null byte
            return text;
        }

        public static void WriteUTF8(this BinaryWriter writer, string value)
        {
            byte[] text = Encoding.UTF8.GetBytes(value);
            writer.Write(text);
        }

        public static void WriteUTF8Z(this BinaryWriter writer, string value)
        {
            writer.WriteUTF8(value);
            writer.Write((byte)0);
        }

        public static string ReadAscii(this BinaryReader reader, int size)
        {
            return Encoding.ASCII.GetString(reader.ReadBytes(size), 0, size);
        }

        public static string ReadUtf8(this BinaryReader reader, int size)
        {
            return Encoding.UTF8.GetString(reader.ReadBytes(size), 0, size);
        }

        public static long MediaToReal(long media)
        {
            return MediaSize * media;
        }

        // https://stackoverflow.com/a/11124118
        public static string GetBytesReadable(long bytes)
        {
            // Get absolute value
            long absBytes = bytes < 0 ? -bytes : bytes;
            // Determine the suffix and readable value
            string suffix;
            double readable;
            if (absBytes >= 0x1000000000000000) // Exabyte
            {
                suffix = "EB";
                readable = bytes >> 50;
            }
            else if (absBytes >= 0x4000000000000) // Petabyte
            {
                suffix = "PB";
                readable = bytes >> 40;
            }
            else if (absBytes >= 0x10000000000) // Terabyte
            {
                suffix = "TB";
                readable = bytes >> 30;
            }
            else if (absBytes >= 0x40000000) // Gigabyte
            {
                suffix = "GB";
                readable = bytes >> 20;
            }
            else if (absBytes >= 0x100000) // Megabyte
            {
                suffix = "MB";
                readable = bytes >> 10;
            }
            else if (absBytes >= 0x400) // Kilobyte
            {
                suffix = "KB";
                readable = bytes;
            }
            else
            {
                return bytes.ToString("0 B"); // Byte
            }
            // Divide by 1024 to get fractional value
            readable = readable / 1024;
            // Return formatted number with suffix
            return readable.ToString("0.### ") + suffix;
        }

        public static long GetNextMultiple(long value, int multiple)
        {
            if (multiple <= 0)
                return value;

            if (value % multiple == 0)
                return value;

            return value + multiple - value % multiple;
        }

        public static int DivideByRoundUp(int value, int divisor) => (value + divisor - 1) / divisor;
        public static long DivideByRoundUp(long value, long divisor) => (value + divisor - 1) / divisor;

        public static int AlignUp(int value, int multiple) => AlignDown(value + multiple - 1, multiple);
        public static long AlignUp(long value, long multiple) => AlignDown(value + multiple - 1, multiple);
        public static int AlignDown(int value, int multiple) => value - value % multiple;
        public static long AlignDown(long value, long multiple) => value - value % multiple;

        public static void IncrementByteArray(byte[] array)
        {
            for (int i = array.Length - 1; i >= 0; i--)
            {
                if (++array[i] != 0)
                    break;
            }
        }

        public static void MemDump(this StringBuilder sb, string prefix, byte[] data)
        {
            int max = 32;
            int remaining = data.Length;
            bool first = true;
            int offset = 0;

            while (remaining > 0)
            {
                max = Math.Min(max, remaining);

                if (first)
                {
                    sb.Append(prefix);
                    first = false;
                }
                else
                {
                    sb.Append(' ', prefix.Length);
                }

                for (int i = 0; i < max; i++)
                {
                    sb.Append($"{data[offset++]:X2}");
                }

                sb.AppendLine();
                remaining -= max;
            }
        }

        public static string GetKeyRevisionSummary(int revision) => revision switch
        {
            0 => "1.0.0-2.3.0",
            1 => "3.0.0",
            2 => "3.0.1-3.0.2",
            3 => "4.0.0-4.1.0",
            4 => "5.0.0-5.1.0",
            5 => "6.0.0-6.0.1",
            6 => "6.2.0",
            7 => "7.0.0-8.0.1",
            8 => "8.1.0-8.1.1",
            9 => "9.0.0-9.0.1",
            0xA => "9.1.0-",
            _ => "Unknown"
        };

        public static bool IsSubRange(long startIndex, long subLength, long length)
        {
            bool isOutOfRange = startIndex < 0 || startIndex > length || subLength < 0 || startIndex > length - subLength;
            return !isOutOfRange;
        }

        public static int GetMasterKeyRevision(int keyGeneration)
        {
            if (keyGeneration == 0) return 0;

            return keyGeneration - 1;
        }

        public static bool IsPowerOfTwo(int value)
        {
            return value > 0 && ResetLeastSignificantOneBit(value) == 0;
        }

        public static bool IsPowerOfTwo(long value)
        {
            return value > 0 && ResetLeastSignificantOneBit(value) == 0;
        }

        public static BigInteger GetBigInteger(this ReadOnlySpan<byte> bytes)
        {
            var signPadded = new byte[bytes.Length + 1];
            bytes.CopyTo(signPadded.AsSpan(1));
            Array.Reverse(signPadded);
            return new BigInteger(signPadded);
        }

        public static byte[] GetBytes(this BigInteger value, int size)
        {
            byte[] bytes = value.ToByteArray();

            if (size == -1)
            {
                size = bytes.Length;
            }

            if (bytes.Length > size + 1)
            {
                throw new InvalidOperationException($"Cannot squeeze value {value} to {size} bytes from {bytes.Length}.");
            }

            if (bytes.Length == size + 1 && bytes[bytes.Length - 1] != 0)
            {
                throw new InvalidOperationException($"Cannot squeeze value {value} to {size} bytes from {bytes.Length}.");
            }

            Array.Resize(ref bytes, size);
            Array.Reverse(bytes);
            return bytes;
        }

        public static BigInteger ModInverse(BigInteger e, BigInteger n)
        {
            BigInteger r = n;
            BigInteger newR = e;
            BigInteger t = 0;
            BigInteger newT = 1;

            while (newR != 0)
            {
                BigInteger quotient = r / newR;
                BigInteger temp;

                temp = t;
                t = newT;
                newT = temp - quotient * newT;

                temp = r;
                r = newR;
                newR = temp - quotient * newR;
            }

            if (t < 0)
            {
                t = t + n;
            }

            return t;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResetLeastSignificantOneBit(int value)
        {
            return value & (value - 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long ResetLeastSignificantOneBit(long value)
        {
            return value & (value - 1);
        }
    }
}
