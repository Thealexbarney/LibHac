// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Common.FixedArrays;

namespace LibHac.Fs.Dbm;

public struct DirectoryName
{
    public Array64<byte> Text;
}

public struct FileName
{
    public Array64<byte> Text;
}

file static class Anonymous
{
    public static void CopyAsDirectoryName(ref DirectoryName destination, ReadOnlySpan<byte> source)
    {
        throw new NotImplementedException();
    }

    public static void CopyAsFileName(ref FileName destination, ReadOnlySpan<byte> source)
    {
        throw new NotImplementedException();
    }

    public static void CopyAsImpl(Span<byte> destination, ReadOnlySpan<byte> source)
    {
        throw new NotImplementedException();
    }

    public static ulong GetLengthImpl(ReadOnlySpan<byte> path)
    {
        throw new NotImplementedException();
    }
}

public class PathTool
{
    public static bool IsSeparator(byte c)
    {
        throw new NotImplementedException();
    }

    public static bool IsCurrentDirectory(ReadOnlySpan<byte> path)
    {
        throw new NotImplementedException();
    }

    public static bool IsParentDirectory(ReadOnlySpan<byte> path)
    {
        throw new NotImplementedException();
    }

    public static bool IsCurrentDirectory(in DirectoryName name, ulong length)
    {
        throw new NotImplementedException();
    }

    public static bool IsParentDirectory(in DirectoryName name, ulong length)
    {
        throw new NotImplementedException();
    }

    public static bool IsEqualDirectoryName(in DirectoryName directoryName1, in DirectoryName directoryName2)
    {
        throw new NotImplementedException();
    }

    public static bool IsEqualFileName(in FileName fileName1, in FileName fileName2)
    {
        throw new NotImplementedException();
    }

    public static ulong GetDirectoryNameLength(in DirectoryName name)
    {
        throw new NotImplementedException();
    }

    public static bool GetDirectoryName(ref DirectoryName outName, ReadOnlySpan<byte> source)
    {
        throw new NotImplementedException();
    }

    public static ulong GetFileNameLength(in FileName name)
    {
        throw new NotImplementedException();
    }

    public static bool GetFileName(ref FileName outName, ReadOnlySpan<byte> source)
    {
        throw new NotImplementedException();
    }

    public static bool ConvertDirectoryNameToFileName(ref FileName outFileName, in DirectoryName directoryName)
    {
        throw new NotImplementedException();
    }

    public static bool ConvertFileNameToDirectoryName(ref DirectoryName outDirectoryName, in FileName fileName)
    {
        throw new NotImplementedException();
    }

    public ref struct PathParser
    {
        private ref byte _previousStartPath;
        private ref byte _previousEndPath;
        private ref byte _nextPath;
        private bool _isParseFinished;
        
        public PathParser()
        {
            throw new NotImplementedException();
        }

        public Result Initialize(ReadOnlySpan<byte> fullPath)
        {
            throw new NotImplementedException();
        }

        public Result FinalizeObject()
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

        public Result GetNextDirectoryName(ref DirectoryName outDirectoryName, out ulong outDirectoryNameLength)
        {
            throw new NotImplementedException();
        }

        public readonly Result GetAsDirectoryName(ref DirectoryName outName, out ulong outNameLength)
        {
            throw new NotImplementedException();
        }

        public readonly Result GetAsFileName(ref FileName outName, out ulong outNameLength)
        {
            throw new NotImplementedException();
        }
    }
}