using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Util;

namespace LibHac.Tools.FsSystem;

public class AesXtsDirectory : IDirectory
{
    private U8String _path;
    private OpenDirectoryMode _mode;

    private IFileSystem _baseFileSystem;
    private UniqueRef<IDirectory> _baseDirectory;

    public AesXtsDirectory(IFileSystem baseFs, ref UniqueRef<IDirectory> baseDir, U8String path, OpenDirectoryMode mode)
    {
        _baseFileSystem = baseFs;
        _baseDirectory = new UniqueRef<IDirectory>(ref baseDir);
        _mode = mode;
        _path = path;
    }

    public override void Dispose()
    {
        _baseDirectory.Destroy();
        base.Dispose();
    }

    protected override Result DoRead(out long entriesRead, Span<DirectoryEntry> entryBuffer)
    {
        Result res = _baseDirectory.Get.Read(out entriesRead, entryBuffer);
        if (res.IsFailure()) return res.Miss();

        for (int i = 0; i < entriesRead; i++)
        {
            ref DirectoryEntry entry = ref entryBuffer[i];

            if (entry.Type == DirectoryEntryType.File)
            {
                if (_mode.HasFlag(OpenDirectoryMode.NoFileSize))
                {
                    entry.Size = 0;
                }
                else
                {
                    string entryName = StringUtils.NullTerminatedUtf8ToString(entry.Name);
                    entry.Size = GetAesXtsFileSize(PathTools.Combine(_path.ToString(), entryName).ToU8Span());
                }
            }
        }

        return Result.Success;
    }

    protected override Result DoGetEntryCount(out long entryCount)
    {
        return _baseDirectory.Get.GetEntryCount(out entryCount);
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
            using var file = new UniqueRef<IFile>();
            Result res = _baseFileSystem.OpenFile(ref file.Ref(), path, OpenMode.Read);
            if (res.IsFailure()) return 0;

            uint magic = 0;
            long fileSize = 0;
            long bytesRead;

            file.Get.Read(out bytesRead, magicOffset, SpanHelpers.AsByteSpan(ref magic), ReadOption.None);
            if (bytesRead != sizeof(uint) || magic != AesXtsFileHeader.AesXtsFileMagic) return 0;

            file.Get.Read(out bytesRead, fileSizeOffset, SpanHelpers.AsByteSpan(ref fileSize), ReadOption.None);
            if (bytesRead != sizeof(long)) return 0;

            return fileSize;
        }
        catch (Exception)
        {
            return 0;
        }
    }
}