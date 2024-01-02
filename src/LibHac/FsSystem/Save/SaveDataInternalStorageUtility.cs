// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Common;
using LibHac.Fs;

namespace LibHac.FsSystem.Save;

public class SaveDataInternalStorageFinder : IInternalStorageFileSystemVisitor
{
    private U8String _name;
    private bool _isFound;

    public SaveDataInternalStorageFinder(U8Span name)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public Result Visit(U8Span name, IStorage storage)
    {
        throw new NotImplementedException();
    }

    public Result Visit(U8Span name, ReadOnlySpan<byte> buffer)
    {
        throw new NotImplementedException();
    }

    public bool IsFound()
    {
        throw new NotImplementedException();
    }

    private void VisitImpl(ReadOnlySpan<byte> name)
    {
        throw new NotImplementedException();
    }
}