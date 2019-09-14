using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using LibHac.Common;

namespace LibHac.Fs
{
    public class LocalDirectory : IDirectory
    {
        private string LocalPath { get; }
        private OpenDirectoryMode Mode { get; }
        private DirectoryInfo DirInfo { get; }
        private IEnumerator<FileSystemInfo> EntryEnumerator { get; }

        public LocalDirectory(LocalFileSystem fs, string path, OpenDirectoryMode mode)
        {
            LocalPath = fs.ResolveLocalPath(path);
            Mode = mode;

            try
            {
                DirInfo = new DirectoryInfo(LocalPath);
            }
            catch (Exception ex) when (ex is ArgumentNullException || ex is ArgumentException ||
                                       ex is PathTooLongException)
            {
                ThrowHelper.ThrowResult(ResultFs.PathNotFound, ex);
                throw;
            }

            if (!DirInfo.Exists)
            {
                ThrowHelper.ThrowResult(ResultFs.PathNotFound);
            }

            EntryEnumerator = DirInfo.EnumerateFileSystemInfos().GetEnumerator();
        }

        public Result Read(out long entriesRead, Span<DirectoryEntry> entryBuffer)
        {
            int i = 0;

            while (i < entryBuffer.Length && EntryEnumerator.MoveNext())
            {
                FileSystemInfo localEntry = EntryEnumerator.Current;
                if (localEntry == null) break;

                bool isDir = localEntry.Attributes.HasFlag(FileAttributes.Directory);

                if (!CanReturnEntry(isDir, Mode)) continue;

                ReadOnlySpan<byte> name = Util.GetUtf8Bytes(localEntry.Name);
                DirectoryEntryType type = isDir ? DirectoryEntryType.Directory : DirectoryEntryType.File;
                long length = isDir ? 0 : ((FileInfo)localEntry).Length;

                StringUtils.Copy(entryBuffer[i].Name, name);
                entryBuffer[i].Name[PathTools.MaxPathLength] = 0;

                entryBuffer[i].Attributes = localEntry.Attributes.ToNxAttributes();
                entryBuffer[i].Type = type;
                entryBuffer[i].Size = length;

                i++;
            }

            entriesRead = i;
            return Result.Success;
        }

        public Result GetEntryCount(out long entryCount)
        {
            int count = 0;

            foreach (FileSystemInfo entry in DirInfo.EnumerateFileSystemInfos())
            {
                bool isDir = (entry.Attributes & FileAttributes.Directory) != 0;

                if (CanReturnEntry(isDir, Mode)) count++;
            }

            entryCount = count;
            return Result.Success;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CanReturnEntry(bool isDir, OpenDirectoryMode mode)
        {
            return isDir && (mode & OpenDirectoryMode.Directory) != 0 ||
                   !isDir && (mode & OpenDirectoryMode.File) != 0;
        }
    }
}
