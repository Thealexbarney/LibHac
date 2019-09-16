using System;

namespace LibHac.FsClient
{
    public interface ICommonMountNameGenerator
    {
        Result Generate(Span<byte> nameBuffer);
    }
}