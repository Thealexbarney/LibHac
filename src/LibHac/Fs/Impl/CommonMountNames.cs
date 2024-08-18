﻿using System;

namespace LibHac.Fs.Impl;

public static class CommonMountNames
{
    public const char ReservedMountNamePrefixCharacter = '@';

    // Filesystem names.
    /// <summary>"<c>@Host</c>"</summary>
    public static ReadOnlySpan<byte> HostRootFileSystemMountName => "@Host"u8;

    /// <summary>"<c>@Local</c>"</summary>
    public static ReadOnlySpan<byte> LocalRootFileSystemMountName => "@Local"u8;

    /// <summary>"<c>@Sdcard</c>"</summary>
    public static ReadOnlySpan<byte> SdCardFileSystemMountName => "@Sdcard"u8;

    /// <summary>"<c>@Gc</c>"</summary>
    public static ReadOnlySpan<byte> GameCardFileSystemMountName => "@Gc"u8;

    /// <summary>"<c>U</c>"</summary>
    public static ReadOnlySpan<byte> GameCardFileSystemMountNameSuffixUpdate => "U"u8;

    /// <summary>"<c>N</c>"</summary>
    public static ReadOnlySpan<byte> GameCardFileSystemMountNameSuffixNormal => "N"u8;

    /// <summary>"<c>S</c>"</summary>
    public static ReadOnlySpan<byte> GameCardFileSystemMountNameSuffixSecure => "S"u8;

    // Built-in storage names.
    /// <summary>"<c>@CalibFile</c>"</summary>
    public static ReadOnlySpan<byte> BisCalibrationFilePartitionMountName => "@CalibFile"u8;

    /// <summary>"<c>@Safe</c>"</summary>
    public static ReadOnlySpan<byte> BisSafeModePartitionMountName => "@Safe"u8;

    /// <summary>"<c>@User</c>"</summary>
    public static ReadOnlySpan<byte> BisUserPartitionMountName => "@User"u8;

    /// <summary>"<c>@System</c>"</summary>
    public static ReadOnlySpan<byte> BisSystemPartitionMountName => "@System"u8;

    /// <summary>"<c>@System0</c>"</summary>
    public static ReadOnlySpan<byte> BisSystemPartition0MountName => "@System0"u8;

    //Content storage names.
    /// <summary>"<c>@SystemContent</c>"</summary>
    public static ReadOnlySpan<byte> ContentStorageSystemMountName => "@SystemContent"u8;

    /// <summary>"<c>@UserContent</c>"</summary>
    public static ReadOnlySpan<byte> ContentStorageUserMountName => "@UserContent"u8;

    /// <summary>"<c>@SdCardContent</c>"</summary>
    public static ReadOnlySpan<byte> ContentStorageSdCardMountName => "@SdCardContent"u8;

    // Registered update partition
    /// <summary>"<c>@RegUpdate</c>"</summary>
    public static ReadOnlySpan<byte> RegisteredUpdatePartitionMountName => "@RegUpdate"u8;
}