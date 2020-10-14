using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Util;

namespace LibHac.FsSystem
{
    public class AesXtsDirectory : IDirectory
    {
        private U8String Path { get; }
        private OpenDirectoryMode Mode { get; }

        private IFileSystem BaseFileSystem { get; }
        private IDirectory BaseDirectory { get; }

        public AesXtsDirectory(IFileSystem baseFs, IDirectory baseDir, U8String path, OpenDirectoryMode mode)
        {
            BaseFileSystem = baseFs;
            BaseDirectory = baseDir;
            Mode = mode;
            Path = path;
        }

        protected override Result DoRead(out long entriesRead, Span<DirectoryEntry> entryBuffer)
        {
            Result rc = BaseDirectory.Read(out entriesRead, entryBuffer);
            if (rc.IsFailure()) return rc;

            for (int i = 0; i < entriesRead; i++)
            {
                ref DirectoryEntry entry = ref entryBuffer[i];

                if (entry.Type == DirectoryEntryType.File)
                {
                    if (Mode.HasFlag(OpenDirectoryMode.NoFileSize))
                    {
                        entry.Size = 0;
                    }
                    else
                    {
                        string entryName = StringUtils.NullTerminatedUtf8ToString(entry.Name);
                        entry.Size = GetAesXtsFileSize(PathTools.Combine(Path.ToString(), entryName).ToU8Span());
                    }
                }
            }

            return Result.Success;
        }

        protected override Result DoGetEntryCount(out long entryCount)
        {
            return BaseDirectory.GetEntryCount(out entryCount);
        }

        /// <summary>
        /// Reads the size of a NAX0 file from its header. Returns 0 on error.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private long GetAesXtsFileSize(U8Span path)
        {
            const long magicOffset = 0x20;
            const long fileSizeOffset = 0x48;

            // Todo: Remove try/catch when more code uses Result
            try
            {
                Result rc = BaseFileSystem.OpenFile(out IFile file, path, OpenMode.Read);
                if (rc.IsFailure()) return 0;

                using (file)
                {
                    uint magic = 0;
                    long fileSize = 0;
                    long bytesRead;

                    file.Read(out bytesRead, magicOffset, SpanHelpers.AsByteSpan(ref magic), ReadOption.None);
                    if (bytesRead != sizeof(uint) || magic != AesXtsFileHeader.AesXtsFileMagic) return 0;

                    file.Read(out bytesRead, fileSizeOffset, SpanHelpers.AsByteSpan(ref fileSize), ReadOption.None);
                    if (bytesRead != sizeof(long) || magic != AesXtsFileHeader.AesXtsFileMagic) return 0;

                    return fileSize;
                }

            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
}
