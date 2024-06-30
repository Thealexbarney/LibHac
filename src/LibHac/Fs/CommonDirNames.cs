using System;

namespace LibHac.Fs;

internal static class CommonDirNames
{
    /// <summary>"<c>Nintendo</c>"</summary>
    public static ReadOnlySpan<byte> SdCardNintendoRootDirectoryName => "Nintendo"u8;

    /// <summary>"<c>Contents</c>"</summary>
    public static ReadOnlySpan<byte> ContentStorageDirectoryName => "Contents"u8;
    
    /// <summary>"<c>Album</c>"</summary>
    public static ReadOnlySpan<byte> ImageDirectoryName => "Album"u8;
}