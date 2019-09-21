using System;
using System.IO;

namespace LibHac.FsSystem.Save
{
    public class SaveDataFile : FileBase
    {
        private AllocationTableStorage BaseStorage { get; }
        private string Path { get; }
        private HierarchicalSaveFileTable FileTable { get; }
        private long Size { get; set; }

        public SaveDataFile(AllocationTableStorage baseStorage, string path, HierarchicalSaveFileTable fileTable, long size, OpenMode mode)
        {
            Mode = mode;
            BaseStorage = baseStorage;
            Path = path;
            FileTable = fileTable;
            Size = size;
        }

        public override Result Read(out long bytesRead, long offset, Span<byte> destination, ReadOption options)
        {
            bytesRead = default;

            int toRead = ValidateReadParamsAndGetSize(destination, offset);

            Result rc = BaseStorage.Read(offset, destination.Slice(0, toRead));
            if (rc.IsFailure()) return rc;

            bytesRead = toRead;
            return Result.Success;
        }

        public override Result Write(long offset, ReadOnlySpan<byte> source, WriteOption options)
        {
            ValidateWriteParams(source, offset);

            BaseStorage.Write(offset, source);

            if ((options & WriteOption.Flush) != 0)
            {
                return Flush();
            }

            return Result.Success;
        }

        public override Result Flush()
        {
            return BaseStorage.Flush();
        }

        public override Result GetSize(out long size)
        {
            size = Size;
            return Result.Success;
        }

        public override Result SetSize(long size)
        {
            if (size < 0) throw new ArgumentOutOfRangeException(nameof(size));
            if (Size == size) return Result.Success;

            Result rc = BaseStorage.SetSize(size);
            if (rc.IsFailure()) return rc;

            if (!FileTable.TryOpenFile(Path, out SaveFileInfo fileInfo))
            {
                throw new FileNotFoundException();
            }

            fileInfo.StartBlock = BaseStorage.InitialBlock;
            fileInfo.Length = size;

            FileTable.AddFile(Path, ref fileInfo);

            Size = size;

            return Result.Success;
        }
    }
}
