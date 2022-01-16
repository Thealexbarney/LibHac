using System;

namespace LibHac.Fs.Impl;

public static class CommonMountNames
{
    public const char ReservedMountNamePrefixCharacter = '@';

    // Filesystem names.
    /// <summary>"<c>@Host</c>"</summary>
    public static ReadOnlySpan<byte> HostRootFileSystemMountName => // "@Host"
            new[] { (byte)'@', (byte)'H', (byte)'o', (byte)'s', (byte)'t' };

    /// <summary>"<c>@Sdcard</c>"</summary>
    public static ReadOnlySpan<byte> SdCardFileSystemMountName => // "@Sdcard"
        new[] { (byte)'@', (byte)'S', (byte)'d', (byte)'c', (byte)'a', (byte)'r', (byte)'d' };

    /// <summary>"<c>@Gc</c>"</summary>
    public static ReadOnlySpan<byte> GameCardFileSystemMountName => // "@Gc"
        new[] { (byte)'@', (byte)'G', (byte)'c' };

    /// <summary>"<c>U</c>"</summary>
    public static ReadOnlySpan<byte> GameCardFileSystemMountNameUpdateSuffix => // "U"
        new[] { (byte)'U' };

    /// <summary>"<c>N</c>"</summary>
    public static ReadOnlySpan<byte> GameCardFileSystemMountNameNormalSuffix => // "N"
        new[] { (byte)'N' };

    /// <summary>"<c>S</c>"</summary>
    public static ReadOnlySpan<byte> GameCardFileSystemMountNameSecureSuffix => // "S"
        new[] { (byte)'S' };

    // Built-in storage names.
    /// <summary>"<c>@CalibFile</c>"</summary>
    public static ReadOnlySpan<byte> BisCalibrationFilePartitionMountName => // "@CalibFile"
        new[]
        {
            (byte)'@', (byte)'C', (byte)'a', (byte)'l', (byte)'i', (byte)'b', (byte)'F', (byte)'i',
            (byte)'l', (byte)'e'
        };

    /// <summary>"<c>@Safe</c>"</summary>
    public static ReadOnlySpan<byte> BisSafeModePartitionMountName => // "@Safe"
        new[] { (byte)'@', (byte)'S', (byte)'a', (byte)'f', (byte)'e' };

    /// <summary>"<c>@User</c>"</summary>
    public static ReadOnlySpan<byte> BisUserPartitionMountName => // "@User"
        new[] { (byte)'@', (byte)'U', (byte)'s', (byte)'e', (byte)'r' };

    /// <summary>"<c>@System</c>"</summary>
    public static ReadOnlySpan<byte> BisSystemPartitionMountName => // "@System"
        new[] { (byte)'@', (byte)'S', (byte)'y', (byte)'s', (byte)'t', (byte)'e', (byte)'m' };

    //Content storage names.
    /// <summary>"<c>@SystemContent</c>"</summary>
    public static ReadOnlySpan<byte> ContentStorageSystemMountName => // "@SystemContent"
        new[]
        {
            (byte)'@', (byte)'S', (byte)'y', (byte)'s', (byte)'t', (byte)'e', (byte)'m', (byte)'C',
            (byte)'o', (byte)'n', (byte)'t', (byte)'e', (byte)'n', (byte)'t'
        };

    /// <summary>"<c>@UserContent</c>"</summary>
    public static ReadOnlySpan<byte> ContentStorageUserMountName => // "@UserContent"
        new[]
        {
            (byte)'@', (byte)'U', (byte)'s', (byte)'e', (byte)'r', (byte)'C', (byte)'o', (byte)'n',
            (byte)'t', (byte)'e', (byte)'n', (byte)'t'
        };

    /// <summary>"<c>@SdCardContent</c>"</summary>
    public static ReadOnlySpan<byte> ContentStorageSdCardMountName => // "@SdCardContent"
        new[]
        {
            (byte)'@', (byte)'S', (byte)'d', (byte)'C', (byte)'a', (byte)'r', (byte)'d', (byte)'C',
            (byte)'o', (byte)'n', (byte)'t', (byte)'e', (byte)'n', (byte)'t'
        };

    // Registered update partition
    /// <summary>"<c>@RegUpdate</c>"</summary>
    public static ReadOnlySpan<byte> RegisteredUpdatePartitionMountName => // "@RegUpdate"
        new[]
        {
            (byte)'@', (byte)'R', (byte)'e', (byte)'g', (byte)'U', (byte)'p', (byte)'d', (byte)'a',
            (byte)'t', (byte)'e'
        };

    /// <summary>"<c>@Nintendo</c>"</summary>
    public static ReadOnlySpan<byte> SdCardNintendoRootDirectoryName => // "Nintendo"
        new[]
        {
            (byte)'N', (byte)'i', (byte)'n', (byte)'t', (byte)'e', (byte)'n', (byte)'d', (byte)'o'
        };
}