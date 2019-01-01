using System;
using System.Buffers;
using System.Collections.Generic;

namespace LibHac.IO
{
    public static class FileSystemExtensions
    {
        // todo add progress logging
        public static void CopyDirectory(this IDirectory source, IDirectory dest)
        {
            IFileSystem sourceFs = source.ParentFileSystem;
            IFileSystem destFs = dest.ParentFileSystem;

            foreach (DirectoryEntry entry in source.Read())
            {
                string subSrcPath = source.FullPath + '/' + entry.Name;
                string subDstPath = dest.FullPath + '/' + entry.Name;

                if (entry.Type == DirectoryEntryType.Directory)
                {
                    destFs.CreateDirectory(subDstPath);
                    IDirectory subSrcDir = sourceFs.OpenDirectory(subSrcPath, OpenDirectoryMode.All);
                    IDirectory subDstDir = destFs.OpenDirectory(subDstPath, OpenDirectoryMode.All);

                    subSrcDir.CopyDirectory(subDstDir);
                }

                if (entry.Type == DirectoryEntryType.File)
                {
                    destFs.CreateFile(subDstPath, entry.Size);

                    using (IFile srcFile = sourceFs.OpenFile(subSrcPath, OpenMode.Read))
                    using (IFile dstFile = destFs.OpenFile(subDstPath, OpenMode.Write))
                    {
                        srcFile.CopyTo(dstFile);
                    }
                }
            }
        }

        public static IEnumerable<DirectoryEntry> EnumerateEntries(this IDirectory directory)
        {
            IFileSystem fs = directory.ParentFileSystem;

            foreach (DirectoryEntry entry in directory.Read())
            {
                yield return entry;
                if (entry.Type != DirectoryEntryType.Directory) continue;

                IDirectory subDir = fs.OpenDirectory(directory.FullPath + '/' + entry.Name, OpenDirectoryMode.All);

                foreach (DirectoryEntry subEntry in subDir.EnumerateEntries())
                {
                    yield return subEntry;
                }
            }
        }

        // todo add progress logging
        public static void CopyTo(this IFile file, IFile dest)
        {
            const int bufferSize = 0x8000;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                long inOffset = 0;

                int bytesRead;
                while ((bytesRead = file.Read(buffer, inOffset)) != 0)
                {
                    dest.Write(buffer.AsSpan(0, bytesRead), inOffset);
                    inOffset += bytesRead;
                }
            }
            finally { ArrayPool<byte>.Shared.Return(buffer); }
        }
    }
}
