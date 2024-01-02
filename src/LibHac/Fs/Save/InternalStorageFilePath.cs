// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Common;
using LibHac.Common.FixedArrays;

namespace LibHac.Fs.Save;

[NonCopyableDisposable]
public ref struct InternalStorageFilePath
{
    private Array64<byte> _fileName;
    private Path _path;

    public InternalStorageFilePath()
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public Result Initialize(U8Span fileName)
    {
        throw new NotImplementedException();
    }

    public readonly ref readonly Path GetPath()
    {
        throw new NotImplementedException();
    }
}