﻿using System;
using System.IO;
using LibHac.Common;
using LibHac.Fs;

namespace LibHac.FsSystem.Save
{
    public class SaveDataFile : FileBase
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

        protected override Result ReadImpl(out long bytesRead, long offset, Span<byte> destination, ReadOption options)
        {
            bytesRead = default;

            Result rc = ValidateReadParams(out long toRead, offset, destination.Length, Mode);
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

        protected override Result WriteImpl(long offset, ReadOnlySpan<byte> source, WriteOption options)
        {
            Result rc = ValidateWriteParams(offset, source.Length, Mode, out bool isResizeNeeded);
            if (rc.IsFailure()) return rc;

            if (isResizeNeeded)
            {
                rc = SetSizeImpl(offset + source.Length);
                if (rc.IsFailure()) return rc;
            }

            rc = BaseStorage.Write(offset, source);
            if (rc.IsFailure()) return rc;

            if ((options & WriteOption.Flush) != 0)
            {
                return Flush();
            }

            return Result.Success;
        }

        protected override Result FlushImpl()
        {
            return BaseStorage.Flush();
        }

        protected override Result GetSizeImpl(out long size)
        {
            size = Size;
            return Result.Success;
        }

        protected override Result SetSizeImpl(long size)
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
