﻿using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Fs;

namespace LibHac.Bcat.Detail.Service.Core
{
    internal class DeliveryCacheDirectoryMetaAccessor
    {
        private const int MaxEntryCount = 100;
        private const int MetaFileHeaderValue = 1;

        private BcatServer Server { get; }
        private object Locker { get; } = new object();
        private DeliveryCacheDirectoryMetaEntry[] Entries { get; } = new DeliveryCacheDirectoryMetaEntry[MaxEntryCount];
        public int Count { get; private set; }

        public DeliveryCacheDirectoryMetaAccessor(BcatServer server)
        {
            Server = server;
        }

        public Result ReadApplicationDirectoryMeta(ulong applicationId, bool allowMissingMetaFile)
        {
            Span<byte> metaPath = stackalloc byte[0x50];
            Server.GetStorageManager().GetDirectoriesMetaPath(metaPath, applicationId);

            return Read(new U8Span(metaPath), allowMissingMetaFile);
        }

        public Result GetEntry(out DeliveryCacheDirectoryMetaEntry entry, int index)
        {
            lock (Locker)
            {
                if (index >= Count)
                {
                    entry = default;
                    return ResultBcat.NotFound.Log();
                }

                entry = Entries[index];
                return Result.Success;
            }
        }

        private Result Read(U8Span path, bool allowMissingMetaFile)
        {
            lock (Locker)
            {
                FileSystemClient fs = Server.GetFsClient();

                Result rc = fs.OpenFile(out FileHandle handle, path, OpenMode.Read);

                if (rc.IsFailure())
                {
                    if (ResultFs.PathNotFound.Includes(rc))
                    {
                        if (allowMissingMetaFile)
                        {
                            Count = 0;
                            return Result.Success;
                        }

                        return ResultBcat.NotFound.LogConverted(rc);
                    }

                    return rc;
                }

                try
                {
                    Count = 0;
                    int header = 0;

                    // Verify the header value
                    rc = fs.ReadFile(out long bytesRead, handle, 0, SpanHelpers.AsByteSpan(ref header));
                    if (rc.IsFailure()) return rc;

                    if (bytesRead != sizeof(int) || header != MetaFileHeaderValue)
                        return ResultBcat.InvalidDeliveryCacheStorageFile.Log();

                    // Read all the directory entries
                    Span<byte> buffer = MemoryMarshal.Cast<DeliveryCacheDirectoryMetaEntry, byte>(Entries);
                    rc = fs.ReadFile(out bytesRead, handle, 4, buffer);
                    if (rc.IsFailure()) return rc;

                    Count = (int)((uint)bytesRead / Unsafe.SizeOf<DeliveryCacheDirectoryMetaEntry>());

                    return Result.Success;
                }
                finally
                {
                    fs.CloseFile(handle);
                }
            }
        }
    }
}
