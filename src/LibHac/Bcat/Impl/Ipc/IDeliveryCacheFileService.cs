using System;

namespace LibHac.Bcat.Impl.Ipc;

public interface IDeliveryCacheFileService : IDisposable
{
    Result Open(ref readonly DirectoryName directoryName, ref readonly FileName fileName);
    Result Read(out long bytesRead, long offset, Span<byte> destination);
    Result GetSize(out long size);
    Result GetDigest(out Digest digest);
}