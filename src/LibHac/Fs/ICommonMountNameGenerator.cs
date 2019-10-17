using System;

namespace LibHac.Fs
{
    public interface ICommonMountNameGenerator
    {
        Result GenerateCommonMountName(Span<byte> nameBuffer);
    }
}