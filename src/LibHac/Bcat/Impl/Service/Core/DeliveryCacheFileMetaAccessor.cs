using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Util;

namespace LibHac.Bcat.Impl.Service.Core;

internal class DeliveryCacheFileMetaAccessor
{
    private const int MaxEntryCount = 100;
    private const int MetaFileHeaderValue = 1;

    private BcatServer Server { get; }
    private object Locker { get; } = new object();
    private DeliveryCacheFileMetaEntry[] Entries { get; } = new DeliveryCacheFileMetaEntry[MaxEntryCount];
    public int Count { get; private set; }

    public DeliveryCacheFileMetaAccessor(BcatServer server)
    {
        Server = server;
    }

    public Result ReadApplicationFileMeta(ulong applicationId, ref DirectoryName directoryName,
        bool allowMissingMetaFile)
    {
        Span<byte> metaPath = stackalloc byte[0x50];
        Server.GetStorageManager().GetFilesMetaPath(metaPath, applicationId, ref directoryName);

        return Read(new U8Span(metaPath), allowMissingMetaFile);
    }

    public Result GetEntry(out DeliveryCacheFileMetaEntry entry, int index)
    {
        UnsafeHelpers.SkipParamInit(out entry);

        lock (Locker)
        {
            if (index >= Count)
            {
                return ResultBcat.NotFound.Log();
            }

            entry = Entries[index];
            return Result.Success;
        }
    }

    public Result FindEntry(out DeliveryCacheFileMetaEntry entry, ref FileName fileName)
    {
        UnsafeHelpers.SkipParamInit(out entry);

        lock (Locker)
        {
            for (int i = 0; i < Count; i++)
            {
                if (StringUtils.CompareCaseInsensitive(Entries[i].Name.Value, fileName.Value) == 0)
                {
                    entry = Entries[i];
                    return Result.Success;
                }
            }

            return ResultBcat.NotFound.Log();
        }
    }

    private Result Read(U8Span path, bool allowMissingMetaFile)
    {
        lock (Locker)
        {
            FileSystemClient fs = Server.GetFsClient();

            Result res = fs.OpenFile(out FileHandle handle, path, OpenMode.Read);

            if (res.IsFailure())
            {
                if (ResultFs.PathNotFound.Includes(res))
                {
                    if (allowMissingMetaFile)
                    {
                        Count = 0;
                        return Result.Success;
                    }

                    return ResultBcat.NotFound.LogConverted(res);
                }

                return res;
            }

            try
            {
                Count = 0;
                int header = 0;

                // Verify the header value
                res = fs.ReadFile(out long bytesRead, handle, 0, SpanHelpers.AsByteSpan(ref header));
                if (res.IsFailure()) return res.Miss();

                if (bytesRead != sizeof(int) || header != MetaFileHeaderValue)
                    return ResultBcat.InvalidDeliveryCacheStorageFile.Log();

                // Read all the file entries
                Span<byte> buffer = MemoryMarshal.Cast<DeliveryCacheFileMetaEntry, byte>(Entries);
                res = fs.ReadFile(out bytesRead, handle, 4, buffer);
                if (res.IsFailure()) return res.Miss();

                Count = (int)((uint)bytesRead / Unsafe.SizeOf<DeliveryCacheFileMetaEntry>());

                return Result.Success;
            }
            finally
            {
                fs.CloseFile(handle);
            }
        }
    }
}