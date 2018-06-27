using System;
using System.IO;
using System.Text;

namespace libhac
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
            progress?.SetTotal((length + bufferSize) / bufferSize);

            int read;
            while ((read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining))) > 0)
            {
                output.Write(buffer, 0, read);
                remaining -= read;
                progress?.ReportAdd(1);
            }
        }

        public static string ReadAsciiZ(this BinaryReader reader)
        {
            var start = reader.BaseStream.Position;

            // Read until we hit the end of the stream (-1) or a zero
            while (reader.BaseStream.ReadByte() - 1 > 0) { }

            int size = (int)(reader.BaseStream.Position - start - 1);
            reader.BaseStream.Position = start;

            string text = reader.ReadAscii(size);
            reader.BaseStream.Position++; // Skip the null byte
            return text;
        }

        public static string ReadAscii(this BinaryReader reader, int size)
        {
            return Encoding.ASCII.GetString(reader.ReadBytes(size), 0, size);
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

        private static int HexToInt(char c)
        {
            switch (c)
            {
                case '0':
                    return 0;
                case '1':
                    return 1;
                case '2':
                    return 2;
                case '3':
                    return 3;
                case '4':
                    return 4;
                case '5':
                    return 5;
                case '6':
                    return 6;
                case '7':
                    return 7;
                case '8':
                    return 8;
                case '9':
                    return 9;
                case 'a':
                case 'A':
                    return 10;
                case 'b':
                case 'B':
                    return 11;
                case 'c':
                case 'C':
                    return 12;
                case 'd':
                case 'D':
                    return 13;
                case 'e':
                case 'E':
                    return 14;
                case 'f':
                case 'F':
                    return 15;
                default:
                    throw new FormatException("Unrecognized hex char " + c);
            }
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
                result[lastcell - (i >> 1)] |= ByteLookup[i & 1, HexToInt(input[lastchar - i])];
            }
            return result;
        }

        internal static long MediaToReal(long media)
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
    }
}
