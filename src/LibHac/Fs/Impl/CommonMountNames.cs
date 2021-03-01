using System;

namespace LibHac.Fs.Impl
{
    public static class CommonMountNames
    {
        public const char ReservedMountNamePrefixCharacter = '@';

        // Filesystem names.
        public static ReadOnlySpan<byte> HostRootFileSystemMountName => // "@Host"
            new[] { (byte)'@', (byte)'H', (byte)'o', (byte)'s', (byte)'t' };

        public static ReadOnlySpan<byte> SdCardFileSystemMountName => // "@Sdcard"
            new[] { (byte)'@', (byte)'S', (byte)'d', (byte)'c', (byte)'a', (byte)'r', (byte)'d' };

        public static ReadOnlySpan<byte> GameCardFileSystemMountName => // "@Gc"
            new[] { (byte)'@', (byte)'G', (byte)'c' };

        public static ReadOnlySpan<byte> GameCardFileSystemMountNameUpdateSuffix => // "U"
            new[] { (byte)'U' };

        public static ReadOnlySpan<byte> GameCardFileSystemMountNameNormalSuffix => // "N"
            new[] { (byte)'N' };

        public static ReadOnlySpan<byte> GameCardFileSystemMountNameSecureSuffix => // "S"
            new[] { (byte)'S' };

        // Built-in storage names.
        public static ReadOnlySpan<byte> BisCalibrationFilePartitionMountName => // "@CalibFile"
            new[]
            {
                (byte)'@', (byte)'C', (byte)'a', (byte)'l', (byte)'i', (byte)'b', (byte)'F', (byte)'i',
                (byte)'l', (byte)'e'
            };

        public static ReadOnlySpan<byte> BisSafeModePartitionMountName => // "@Safe"
            new[] { (byte)'@', (byte)'S', (byte)'a', (byte)'f', (byte)'e' };

        public static ReadOnlySpan<byte> BisUserPartitionMountName => // "@User"
            new[] { (byte)'@', (byte)'U', (byte)'s', (byte)'e', (byte)'r' };

        public static ReadOnlySpan<byte> BisSystemPartitionMountName => // "@System"
            new[] { (byte)'@', (byte)'S', (byte)'y', (byte)'s', (byte)'t', (byte)'e', (byte)'m' };

        //Content storage names.
        public static ReadOnlySpan<byte> ContentStorageSystemMountName => // "@SystemContent"
            new[]
            {
                (byte)'@', (byte)'S', (byte)'y', (byte)'s', (byte)'t', (byte)'e', (byte)'m', (byte)'C',
                (byte)'o', (byte)'n', (byte)'t', (byte)'e', (byte)'n', (byte)'t'
            };

        public static ReadOnlySpan<byte> ContentStorageUserMountName => // "@UserContent"
            new[]
            {
                (byte)'@', (byte)'U', (byte)'s', (byte)'e', (byte)'r', (byte)'C', (byte)'o', (byte)'n',
                (byte)'t', (byte)'e', (byte)'n', (byte)'t'
            };

        public static ReadOnlySpan<byte> ContentStorageSdCardMountName => // "@SdCardContent"
            new[]
            {
                (byte)'@', (byte)'S', (byte)'d', (byte)'C', (byte)'a', (byte)'r', (byte)'d', (byte)'C',
                (byte)'o', (byte)'n', (byte)'t', (byte)'e', (byte)'n', (byte)'t'
            };

        // Registered update partition
        public static ReadOnlySpan<byte> RegisteredUpdatePartitionMountName => // "@RegUpdate"
            new[]
            {
                (byte)'@', (byte)'R', (byte)'e', (byte)'g', (byte)'U', (byte)'p', (byte)'d', (byte)'a',
                (byte)'t', (byte)'e'
            };

        public static ReadOnlySpan<byte> SdCardNintendoRootDirectoryName => // "Nintendo"
            new[]
            {
                (byte)'N', (byte)'i', (byte)'n', (byte)'t', (byte)'e', (byte)'n', (byte)'d', (byte)'o'
            };
    }
}
