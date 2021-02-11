using System;

namespace LibHac.Fs.Fsa
{
    public interface ICommonMountNameGenerator : IDisposable
    {
        Result GenerateCommonMountName(Span<byte> nameBuffer);
    }

    public interface ISaveDataAttributeGetter : IDisposable
    {
        Result GetSaveDataAttribute(out SaveDataAttribute attribute);
    }
}

