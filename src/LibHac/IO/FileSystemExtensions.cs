using System;
using System.Collections.Generic;
using System.IO;

namespace LibHac.IO
{
    public static class FileSystemExtensions
    {
        public static void Extract(this IFileSystem fs, string outDir)
        {
            IDirectory root = fs.OpenDirectory("/", OpenDirectoryMode.All);

            foreach (string filename in root.EnumerateFiles())
            {
                //Console.WriteLine(filename);
                IFile file = fs.OpenFile(filename);
                string outPath = Path.Combine(outDir, filename.TrimStart('/'));

                string directoryName = Path.GetDirectoryName(outPath);
                if(!string.IsNullOrWhiteSpace(directoryName)) Directory.CreateDirectory(directoryName);

                using (var outFile = new FileStream(outPath, FileMode.Create, FileAccess.ReadWrite))
                {
                    file.CopyTo(outFile);
                }
            }
        }

        public static IEnumerable<string> EnumerateFiles(this IDirectory directory)
        {
            DirectoryEntry[] entries = directory.Read();

            foreach (DirectoryEntry entry in entries)
            {
                if (entry.Type == DirectoryEntryType.Directory)
                {
                    foreach (string a in EnumerateFiles(directory.ParentFileSystem.OpenDirectory(entry.Name, OpenDirectoryMode.All)))
                    {
                        yield return a;
                    }
                }

                if (entry.Type == DirectoryEntryType.File)
                {
                    yield return entry.Name;
                }
            }
        }

        public static void CopyTo(this IFile file, Stream output)
        {
            const int bufferSize = 0x8000;
            long remaining = file.GetSize();
            long inOffset = 0;
            var buffer = new byte[bufferSize];

            while (remaining > 0)
            {
                int toWrite = (int)Math.Min(buffer.Length, remaining);
                file.Read(buffer.AsSpan(0, toWrite), inOffset);

                output.Write(buffer, 0, toWrite);
                remaining -= toWrite;
                inOffset += toWrite;
            }
        }
    }
}
