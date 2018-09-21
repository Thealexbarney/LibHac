using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LibHac
{
    public static class Util
    {
        private const int MediaSize = 0x200;

        public static T CreateJaggedArray<T>(params int[] lengths)
        {
            return (T)InitializeJaggedArray(typeof(T).GetElementType(), 0, lengths);
        }

        private static object InitializeJaggedArray(Type type, int index, int[] lengths)
        {
            Array array = Array.CreateInstance(type, lengths[index]);

            Type elementType = type.GetElementType();
            if (elementType == null) return array;

            for (int i = 0; i < lengths[index]; i++)
            {
                array.SetValue(InitializeJaggedArray(elementType, index + 1, lengths), i);
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

        public static bool IsEmpty(this byte[] array)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));

            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] != 0)
                {
                    return false;
                }
            }

            return true;
        }

        public static void CopyStream(this Stream input, Stream output, long length, IProgressReport progress = null)
        {
            const int bufferSize = 0x8000;
            long remaining = length;
            byte[] buffer = new byte[bufferSize];
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

        public static string ReadAsciiZ(this BinaryReader reader, int maxLength = int.MaxValue)
        {
            var start = reader.BaseStream.Position;
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
            var start = reader.BaseStream.Position;
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

        // todo Maybe make less naive
        public static string GetRelativePath(string path, string basePath)
        {
            var directory = new DirectoryInfo(basePath);
            var file = new FileInfo(path);

            string fullDirectory = directory.FullName;
            string fullFile = file.FullName;

            if (!fullFile.StartsWith(fullDirectory))
            {
                throw new ArgumentException($"{nameof(path)} is not a subpath of {nameof(basePath)}");
            }

            return fullFile.Substring(fullDirectory.Length + 1);
        }

        private static bool TryHexToInt(char c, out int value)
        {
            switch (c)
            {
                case '0':
                    value = 0; break;
                case '1':
                    value = 1; break;
                case '2':
                    value = 2; break;
                case '3':
                    value = 3; break;
                case '4':
                    value = 4; break;
                case '5':
                    value = 5; break;
                case '6':
                    value = 6; break;
                case '7':
                    value = 7; break;
                case '8':
                    value = 8; break;
                case '9':
                    value = 9; break;
                case 'a':
                case 'A':
                    value = 10; break;
                case 'b':
                case 'B':
                    value = 11; break;
                case 'c':
                case 'C':
                    value = 12; break;
                case 'd':
                case 'D':
                    value = 13; break;
                case 'e':
                case 'E':
                    value = 14; break;
                case 'f':
                case 'F':
                    value = 15; break;
                default:
                    value = 0;
                    return false;
            }

            return true;
        }

        private static readonly byte[,] ByteLookup = {
            {0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f},
            {0x00, 0x10, 0x20, 0x30, 0x40, 0x50, 0x60, 0x70, 0x80, 0x90, 0xa0, 0xb0, 0xc0, 0xd0, 0xe0, 0xf0}
        };

        public static byte[] ToBytes(this string input)
        {
            var result = new byte[(input.Length + 1) >> 1];
            int lastcell = result.Length - 1;
            int lastchar = input.Length - 1;
            for (int i = 0; i < input.Length; i++)
            {
                if (!TryHexToInt(input[lastchar - i], out int hexInt))
                {
                    throw new FormatException($"Unrecognized hex char {input[lastchar - i]}");
                }

                result[lastcell - (i >> 1)] |= ByteLookup[i & 1, hexInt];
            }
            return result;
        }

        public static bool TryToBytes(this string input, out byte[] bytes)
        {
            var result = new byte[(input.Length + 1) >> 1];
            int lastcell = result.Length - 1;
            int lastchar = input.Length - 1;
            for (int i = 0; i < input.Length; i++)
            {
                if (!TryHexToInt(input[lastchar - i], out int hexInt))
                {
                    bytes = null;
                    return false;
                }

                result[lastcell - (i >> 1)] |= ByteLookup[i & 1, hexInt];
            }
            bytes = result;
            return true;
        }

        private static readonly uint[] Lookup32 = CreateLookup32();

        private static uint[] CreateLookup32()
        {
            var result = new uint[256];
            for (int i = 0; i < 256; i++)
            {
                string s = i.ToString("X2");
                result[i] = s[0] + ((uint)s[1] << 16);
            }
            return result;
        }

        public static string ToHexString(this byte[] bytes)
        {
            var lookup32 = Lookup32;
            var result = new char[bytes.Length * 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                var val = lookup32[bytes[i]];
                result[2 * i] = (char)val;
                result[2 * i + 1] = (char)(val >> 16);
            }
            return new string(result);
        }

        public static long MediaToReal(long media)
        {
            return MediaSize * media;
        }

        public static string GetBytesReadable(long bytes)
        {
            // Get absolute value
            long absBytes = (bytes < 0 ? -bytes : bytes);
            // Determine the suffix and readable value
            string suffix;
            double readable;
            if (absBytes >= 0x1000000000000000) // Exabyte
            {
                suffix = "EB";
                readable = (bytes >> 50);
            }
            else if (absBytes >= 0x4000000000000) // Petabyte
            {
                suffix = "PB";
                readable = (bytes >> 40);
            }
            else if (absBytes >= 0x10000000000) // Terabyte
            {
                suffix = "TB";
                readable = (bytes >> 30);
            }
            else if (absBytes >= 0x40000000) // Gigabyte
            {
                suffix = "GB";
                readable = (bytes >> 20);
            }
            else if (absBytes >= 0x100000) // Megabyte
            {
                suffix = "MB";
                readable = (bytes >> 10);
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
            readable = (readable / 1024);
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
            var remaining = data.Length;
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

        public static string GetKeyRevisionSummary(int revision)
        {
            switch (revision)
            {
                case 0: return "1.0.0-2.3.0";
                case 1: return "3.0.0";
                case 2: return "3.0.1-3.0.2";
                case 3: return "4.0.0-4.1.0";
                case 4: return "5.0.0-5.1.0";
                case 5: return "6.0.0";
                default: return "Unknown";
            }
        }
    }

    public class ByteArray128BitComparer : EqualityComparer<byte[]>
    {
        public override bool Equals(byte[] first, byte[] second)
        {
            if (first == null || second == null)
            {
                // null == null returns true.
                // non-null == null returns false.
                return first == second;
            }
            if (ReferenceEquals(first, second))
            {
                return true;
            }
            if (first.Length != second.Length)
            {
                return false;
            }
            // Linq extension method is based on IEnumerable, must evaluate every item.
            return first.SequenceEqual(second);
        }

        public override int GetHashCode(byte[] obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }
            if (obj.Length != 16)
            {
                throw new ArgumentException("Length must be 16 bytes");
            }

            var hi = BitConverter.ToUInt64(obj, 0);
            var lo = BitConverter.ToUInt64(obj, 8);

            return (hi.GetHashCode() * 397) ^ lo.GetHashCode();
        }

    }
}
