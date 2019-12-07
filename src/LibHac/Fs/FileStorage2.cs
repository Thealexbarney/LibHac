﻿using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace LibHac.Fs
{
    public class FileStorage2 : StorageBase
    {
        private const long InvalidSize = -1;

        private IFile BaseFile { get; set; }
        private long FileSize { get; set; }

        public FileStorage2(IFile baseFile)
        {
            BaseFile = baseFile;
            FileSize = InvalidSize;
        }

        protected FileStorage2() { }

        protected void SetFile(IFile file)
        {
            Debug.Assert(file != null);
            Debug.Assert(BaseFile == null);

            BaseFile = file;
        }

        private Result UpdateSize()
        {
            if (FileSize != InvalidSize)
                return Result.Success;

            Result rc = BaseFile.GetSize(out long fileSize);
            if (rc.IsFailure()) return rc;

            FileSize = fileSize;
            return Result.Success;
        }

        protected override Result ReadImpl(long offset, Span<byte> destination)
        {
            if (destination.Length == 0)
                return Result.Success;

            Result rc = UpdateSize();
            if (rc.IsFailure()) return rc;

            if (!IsRangeValid(offset, destination.Length, FileSize))
                return ResultFs.ValueOutOfRange.Log();

            return BaseFile.Read(out _, offset, destination, ReadOption.None);
        }

        protected override Result WriteImpl(long offset, ReadOnlySpan<byte> source)
        {
            if (source.Length == 0)
                return Result.Success;

            Result rc = UpdateSize();
            if (rc.IsFailure()) return rc;

            if (!IsRangeValid(offset, source.Length, FileSize))
                return ResultFs.ValueOutOfRange.Log();

            return BaseFile.Write(offset, source, WriteOption.None);
        }

        protected override Result FlushImpl()
        {
            return BaseFile.Flush();
        }

        protected override Result GetSizeImpl(out long size)
        {
            Result rc = UpdateSize();
            if (rc.IsFailure())
            {
                size = default;
                return rc;
            }

            size = FileSize;
            return Result.Success;
        }

        protected override Result SetSizeImpl(long size)
        {
            FileSize = InvalidSize;
            return BaseFile.SetSize(size);
        }

        protected override Result OperateRangeImpl(Span<byte> outBuffer, OperationId operationId, long offset, long size, ReadOnlySpan<byte> inBuffer)
        {
            switch (operationId)
            {
                case OperationId.InvalidateCache:
                case OperationId.QueryRange:
                    if (size == 0)
                    {
                        if (operationId == OperationId.QueryRange)
                        {
                            if (outBuffer.Length != Unsafe.SizeOf<QueryRangeInfo>())
                            {
                                return ResultFs.InvalidSize.Log();
                            }

                            Unsafe.As<byte, QueryRangeInfo>(ref outBuffer[0]) = new QueryRangeInfo();
                        }

                        return Result.Success;
                    }

                    Result rc = UpdateSize();
                    if (rc.IsFailure()) return rc;

                    if (size < 0 || offset < 0)
                    {
                        return ResultFs.ValueOutOfRange.Log();
                    }

                    return BaseFile.OperateRange(outBuffer, operationId, offset, size, inBuffer);

                default:
                    return ResultFs.UnsupportedOperationInFileStorageOperateRange.Log();
            }
        }
    }
}
