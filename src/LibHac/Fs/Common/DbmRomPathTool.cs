using System;

// ReSharper disable once CheckNamespace
namespace LibHac.Fs;

public static class RomPathTool
{
    public static bool IsEqualPath(ReadOnlySpan<byte> path1, ReadOnlySpan<byte> path2, int length)
    {
        throw new NotImplementedException();
    }
    
    public ref struct PathParser
    {
        public PathParser()
        {
            throw new NotImplementedException();
        }

        public Result Initialize(ReadOnlySpan<byte> fullPath)
        {
            throw new NotImplementedException();
        }

        public void FinalizeObject()
        {
            throw new NotImplementedException();
        }

        public readonly bool IsParseFinished()
        {
            throw new NotImplementedException();
        }

        public readonly bool IsDirectoryPath()
        {
            throw new NotImplementedException();
        }

        public Result GetNextDirectoryName(out RomEntryName outName)
        {
            throw new NotImplementedException();
        }

        public readonly Result GetAsDirectoryName(out RomEntryName outName)
        {
            throw new NotImplementedException();
        }

        public readonly Result GetAsFileName(out RomEntryName outName)
        {
            throw new NotImplementedException();
        }
    }

    public ref struct RomEntryName
    {
        private ReadOnlySpan<byte> _path;

        public RomEntryName()
        {
            _path = default;
        }

        public void Initialize(ReadOnlySpan<byte> path)
        {
            _path = path;
        }

        public readonly bool IsCurrentDirectory()
        {
            return _path.Length == 1 && _path[0] == (byte)'.';
        }

        public readonly bool IsParentDirectory()
        {
            return _path.Length == 2 && _path[0] == (byte)'.' && _path[1] == (byte)'.';
        }

        public readonly bool IsRootDirectory()
        {
            return _path.Length == 0;
        }

        public readonly ReadOnlySpan<byte> GetPath()
        {
            return _path;
        }
    }
}