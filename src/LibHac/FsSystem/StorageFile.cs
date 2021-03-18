using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem
{
    public class StorageFile : IFile
    {
        private IStorage BaseStorage { get; }
        private OpenMode Mode { get; }

        public StorageFile(IStorage baseStorage, OpenMode mode)
        {
            BaseStorage = baseStorage;
            Mode = mode;
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
            if (!Mode.HasFlag(OpenMode.Write))
                return Result.Success;

            return BaseStorage.Flush();
        }

        protected override Result DoGetSize(out long size)
        {
            return BaseStorage.GetSize(out size);
        }

        protected override Result DoSetSize(long size)
        {
            if (!Mode.HasFlag(OpenMode.Write))
                return ResultFs.WriteUnpermitted.Log();

            return BaseStorage.SetSize(size);
        }

        protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
            ReadOnlySpan<byte> inBuffer)
        {
            return ResultFs.NotImplemented.Log();
        }
    }
}
