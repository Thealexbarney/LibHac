using System;
using System.IO;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem.Save
{
    public class SaveDataFile : IFile
    {
        private AllocationTableStorage BaseStorage { get; }
        private U8String Path { get; }
        private HierarchicalSaveFileTable FileTable { get; }
        private long Size { get; set; }
        private OpenMode Mode { get; }

        public SaveDataFile(AllocationTableStorage baseStorage, U8Span path, HierarchicalSaveFileTable fileTable, long size, OpenMode mode)
        {
            Mode = mode;
            BaseStorage = baseStorage;
            Path = path.ToU8String();
            FileTable = fileTable;
            Size = size;
        }

        protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination,
            in ReadOption option)
        {
            UnsafeHelpers.SkipParamInit(out bytesRead);

            Result rc = DryRead(out long toRead, offset, destination.Length, in option, Mode);
            if (rc.IsFailure()) return rc;

            if (toRead == 0)
            {
                bytesRead = 0;
                return Result.Success;
            }

            rc = BaseStorage.Read(offset, destination.Slice(0, (int)toRead));
            if (rc.IsFailure()) return rc;

            bytesRead = toRead;
            return Result.Success;
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
        {
            Result rc = DryWrite(out bool isResizeNeeded, offset, source.Length, in option, Mode);
            if (rc.IsFailure()) return rc;

            if (isResizeNeeded)
            {
                rc = DoSetSize(offset + source.Length);
                if (rc.IsFailure()) return rc;
            }

            rc = BaseStorage.Write(offset, source);
            if (rc.IsFailure()) return rc;

            if (option.HasFlushFlag())
            {
                return Flush();
            }

            return Result.Success;
        }

        protected override Result DoFlush()
        {
            return BaseStorage.Flush();
        }

        protected override Result DoGetSize(out long size)
        {
            size = Size;
            return Result.Success;
        }

        protected override Result DoSetSize(long size)
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

        protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size, ReadOnlySpan<byte> inBuffer)
        {
            return ResultFs.NotImplemented.Log();
        }
    }
}
